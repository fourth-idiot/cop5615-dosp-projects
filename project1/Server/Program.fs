#if INTERACTIVE
#r "nuget: Akka.FSharp"
#r "nuget: Akka.Remote"
#endif


open System
open System.Text
open System.Security.Cryptography
open Akka
open Akka.FSharp
open Akka.Remote
open Akka.Routing
open System.Diagnostics
open System.Threading


// let system = System.create "my-system" (Configuration.load())


// Bitcoin counter
let printBitcoin (str: string) (count: int) =
    let result = str.Split '|'
    let inputString = result.[0]
    let hashValue = result.[1]
    printfn "%s\t%s" inputString hashValue
    count + 1

let bitcoinCounter f initialState (mailbox: Actor<'a>) = 
    let rec loop lastState = actor {
        let! message = mailbox.Receive()
        let newState = f message lastState
        return! loop newState
    }
    loop initialState

// // Code to test Bitcoin collector in dotnet fsi mode
// let bitcoinCounterRef =
//     bitcoinCounter printBitcoin 0
//     |> spawn system "bitcoinCounter"
// bitcoinCounterRef <! "n.saojiRandomString|3662f04d36c9a80629f4ff6d60cfacce94a564f12333c974f3e5af815c6527d0"


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
        bitcoin <- "[Server]" + inputString + "|" + hashValue
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

let worker noOfLeadingZeroes prefix bitcoinCounterRef (mailbox: Actor<'a>) = 
    let rec loop () = actor {
        let! (systemIdentifier, startNo, endNo) = mailbox.Receive ()
        let sender = mailbox.Sender()
        findBitcoins systemIdentifier startNo endNo noOfLeadingZeroes prefix bitcoinCounterRef
        sender <! "DoneProcessing"
        return! loop ()
    }
    loop ()

// // Code to test Worker in dotnet fsi mode
// // 1. Single worker
// let prefix = "n.saoji"
// let noOfLeadingZeroes = 1
// let workerRef =
//     worker noOfLeadingZeroes prefix bitcoinCounterRef
//     |> spawn system "worker"
// workerRef <! ("server", 0, 100)

// // 2. Worker router
// let prefix = "n.saoji"
// let noOfLeadingZeroes = 1
// let totalWork = 10000
// let workUnit = 10
// let noOfWorkers = Environment.ProcessorCount
// let workerRef =
//     worker noOfLeadingZeroes prefix bitcoinCounterRef
//     |> spawnOpt system "worker"
//     <| [SpawnOption.Router(RoundRobinPool(noOfWorkers))]
// for i = 0 to (totalWork / workUnit) do
//     workerRef <! ("server", workUnit * i, workUnit * (i + 1))


// Master
let randomStringGenerator length =
    let random = Random()
    let chars = Array.concat([[|'a' .. 'z'|]; [|'A' .. 'Z'|]; [|'0' .. '9'|]])
    seq {
        for _ = 1 to length do
            yield chars.[random.Next(chars.Length)]
    } |> String.Concat

let master totalWork workUnit noOfLeadingZeroes noOfWorkers prefix workerRef (mailbox: Actor<'a>) =
    let systemIdentifier = randomStringGenerator(8)
    let mutable i = 0
    let totalNoOfSteps = totalWork / workUnit
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        let sender = mailbox.Sender ()
        match message with
        | "MessageFromSelf" ->
            printfn "[INFO] Call from the self"
            while ((i < noOfWorkers) && (i < totalNoOfSteps)) do
                let startNo = (workUnit * i) + 1
                let endNo = workUnit * (i + 1)
                workerRef <! (systemIdentifier, startNo, endNo)
                i <- i + 1
        | "DoneProcessing" ->
            if (i < totalNoOfSteps) then
                let startNo = (workUnit * i) + 1
                let endNo = workUnit * (i + 1)
                sender <! (systemIdentifier, startNo, endNo)
                i <- i + 1
        | "MessageFromClient" ->
            printfn "[INFO] Call from the client"
            let clientString = (string noOfLeadingZeroes) + "|" + prefix
            sender <! clientString
        | _ -> ()
        return! loop ()
    }
    loop()
    
// // Code to test Master in dotnet fsi mode
// let prefix = "n.saoji"
// let noOfLeadingZeroes = 1
// let totalWork = 10000
// let workUnit = 10
// let noOfWorkers = Environment.ProcessorCount
// let masterRef =
//     master totalWork workUnit noOfLeadingZeroes noOfWorkers prefix workerRef
//     |> spawn system "master"
// masterRef <! "MessageFromSelf"


[<EntryPoint>]
let main argv =
    Console.Title <- sprintf "[COP5615] DOSP Project 1 Server : %d" (Process.GetCurrentProcess().Id)

    // Server configurations
    let config =
        Configuration.parse
            @"akka {
                actor {
                    provider = ""Akka.Remote.RemoteActorRefProvider, Akka.Remote""
                }
                remote.helios.tcp {
                        port = 8080
                        hostname = localhost
                    }
                }
            }"
    let system = System.create "Server" config

    // Read number of leading zeroes from the command line arguments
    let noOfLeadingZeroes =
        try
            int argv.[0]
        with
            | _ ->
                printfn "[INFO] Incorrect/Missing leading number of zeroes, taking the default value as 4"
                4
    printfn "[INFO] Number of leading zeroes: %i" noOfLeadingZeroes

    // Decide number of workers depending upon number of workers
    let noOfWorkers = Environment.ProcessorCount * 500
    printfn "[INFO] Number of workers: %d" noOfWorkers

    // Add gatorlink as prefix to the input string
    let prefix = "n.saoji"
    // let prefix = "parcha.srikanthr"
    printfn "[INFO] Prefix: %s" prefix

    // Spawn all the actors in order of their dependency on other actors
    // 1. Bitcoin collector
    let bitcoinCounterRef =
        bitcoinCounter printBitcoin 0
        |> spawn system "bitcoinCounter"

    // Total work and work unit size
    let totalWork = 1000000000
    let workUnit = 250
    // 2. Worker
    let workerRef =
        worker noOfLeadingZeroes prefix bitcoinCounterRef
        |> spawnOpt system "worker"
        <| [SpawnOption.Router(RoundRobinPool(noOfWorkers))]

    // 3. Master
    let masterRef =
        master totalWork workUnit noOfLeadingZeroes noOfWorkers prefix workerRef
        |> spawn system "master"

    // Entrypoint - Comment this code while testing
    masterRef <! "MessageFromSelf"
    Thread.Sleep(1000000000)
    0

    // // Uncomment this for code performance evaluation
    // let proc = Process.GetCurrentProcess()
    // let cpuTimeStart = proc.TotalProcessorTime
    // let timer = Stopwatch()
    // timer.Start()
    // try
    //     masterRef <! "MessageFromSelf"
    //     Thread.Sleep(60000)
    // finally
    //     let cpuTime = (proc.TotalProcessorTime - cpuTimeStart).TotalMilliseconds
    //     printfn "CPU time = %d ms" (int cpuTime)
    //     printfn "Actual time = %d ms" timer.ElapsedMilliseconds
    //     printfn "Ratio = %f" (cpuTime / float(timer.ElapsedMilliseconds))
    // 0