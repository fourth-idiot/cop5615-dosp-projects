#if INTERACTIVE
#r "nuget: Akka.FSharp"
#r "nuget: Akka.Remote"
#endif


open System
open System.Text
open System.Security.Cryptography
open Akka.FSharp
open Akka.Remote
open Akka.Routing
open System.Threading

 
// Worker
let generateHash (inputString: string) =
    let hashValue =
        inputString
        |> Encoding.UTF8.GetBytes
        |> (new SHA256Managed()).ComputeHash
        |> Array.map (fun (x : byte) -> String.Format("{0:X2}", x))
        |> String.concat String.Empty
    hashValue

let checkValidbitcoin inputString hashValue noOfLeadingZeroes =
    let compareString = String('0', noOfLeadingZeroes)
    let mutable bitcoin = ""
    if (string hashValue).StartsWith compareString then
        bitcoin <- "[Client]" + inputString + "|" + hashValue
    else
        bitcoin <- "-1"
    bitcoin

let findBitcoins systemIdentifier startNo endNo noOfLeadingZeroes prefix bitcoinCounterRef =
    for nonce = startNo to endNo do
        let inputString = prefix + ";" + systemIdentifier + string nonce
        let hashValue = generateHash inputString
        let bitcoin = checkValidbitcoin inputString hashValue noOfLeadingZeroes 
        if bitcoin <> "-1" then
            bitcoinCounterRef <! bitcoin

let worker bitcoinCounterRef (mailbox: Actor<'a>) = 
    let rec loop () = actor {
        let! (noOfLeadingZeroes, prefix, systemIdentifier, startNo, endNo) = mailbox.Receive ()
        findBitcoins systemIdentifier startNo endNo noOfLeadingZeroes prefix bitcoinCounterRef
        return! loop ()
    }
    loop ()


// Master
let randomStringGenerator length =
    let random = Random()
    let chars = Array.concat([[|'a' .. 'z'|]; [|'A' .. 'Z'|]; [|'0' .. '9'|]])
    seq {
        for _ = 1 to length do
            yield chars.[random.Next(chars.Length)]
    } |> String.Concat

let master totalWork workUnit serverMasterRef workerRef (mailbox: Actor<'a>) =
    let rec loop () = actor {
        let systemIdentifier = randomStringGenerator(8)
        let! message = mailbox.Receive()
        match box message with
        | :? string as msg when msg="GetWorkFromServer" -> 
            printfn "[INFO] Client has joined. Getting work from server..."
            serverMasterRef <! "MessageFromClient"
        | :? string as msg when (msg |> String.exists (fun char -> char = '|')) ->
            printfn "[INFO] Received work from the server"
            let result = msg.Split '|'
            let noOfLeadingZeroes = int result.[0]
            let prefix = result.[1]
            for i = 0 to ((totalWork / workUnit) - 1) do
                workerRef <! (noOfLeadingZeroes, prefix, systemIdentifier, (workUnit * i), (workUnit * (i + 1)))
        | _ -> ()
        return! loop ()
    }
    loop ()

    
[<EntryPoint>]
let main argv =
    Console.Title <- sprintf "[COP5615] DOSP Project 1 Client : %d" (Diagnostics.Process.GetCurrentProcess().Id)

    // Client configurations
    let config =
        Configuration.parse
            @"akka {
                actor {
                    provider = ""Akka.Remote.RemoteActorRefProvider, Akka.Remote""
                }
                remote.helios.tcp {
    		            port = 0
    		            hostname = localhost
                    }
                }
            }"
    let system = System.create "Client" config

    // Read IP address
    // Read number of leading zeroes from the command line arguments
    let ipAddress =
        try
            string argv.[0]
        with
            | _ ->
                printfn "[INFO] Incorrect/Missing IP Address, taking the default value as localhost"
                "localhost"
    printfn "[INFO] Server IP address: %s" ipAddress

    // Select server actors
    let serverMasterUrl = "akka.tcp://Server@" + ipAddress + ":8080/user/master"
    let serverMasterRef = select serverMasterUrl system
    let serverBitcoinCounterUrl = "akka.tcp://Server@" + ipAddress + ":8080/user/bitcoinCounter"
    let serverBitcoinCounterRef = select serverBitcoinCounterUrl system

    // Specify number of client workers
    let noOfWorkers = Environment.ProcessorCount * 500
    printfn "[INFO] Number of workers: %d" noOfWorkers

    // Total work and work unit size
    let totalWork = 1000000000
    let workUnit = 250
    // Worker
    let workerRef =
        worker serverBitcoinCounterRef
        |> spawnOpt system "worker"
        <| [SpawnOption.Router(RoundRobinPool(noOfWorkers))]

    let masterRef =
        master totalWork workUnit serverMasterRef workerRef
        |> spawn system "master"

    masterRef <! "GetWorkFromServer"
    Thread.Sleep(1000000000)
    0 // return an integer exit code
