open System
open Akka.FSharp
open FSharp.Json
open WebSocketSharp


type clientMessages =
    | Register of (string * string)
    | Login of (string * string)
    | Logout
    | Follow of (string)
    | Tweet of (string)
    | Retweet of (int)
    | QueryTweetsSubscribedTo
    | QueryTweetsWithHashtag of (string)
    | QueryTweetsWithMentions


type JsonType = {
    command : string
    username : string
    password : string
    following : string
    tweet : string
    tweetId : int
    hashtag : string
}


let clientActor (server: WebSocket) (mailbox: Actor<_>) = 

    let mutable username = ""

    let rec loop () = actor {
        let! message = mailbox.Receive ()
        // Handle message here
        match message with
        | Register(u, p) ->
            let json : JsonType = {
                command = "Register";
                username = u;
                password = p;
                following = "";
                tweet = "";
                tweetId = -1;
                hashtag = ""
            }
            let serializedJson = Json.serialize json
            server.Send serializedJson
            username <- u

        | Login(u, p) ->
            let json : JsonType = {
                command = "Login";
                username = u;
                password = p;
                following = "";
                tweet = "";
                tweetId = -1;
                hashtag = ""
            }
            let serializedJson = Json.serialize json
            server.Send serializedJson
            username <- u
            

        | Logout ->
            let json : JsonType = {
                command = "Logout";
                username = username;
                password = "";
                following = "";
                tweet = "";
                tweetId = -1;
                hashtag = ""
            }
            let serializedJson = Json.serialize json
            server.Send serializedJson
            username <- ""

        | Follow(following) ->
            let json : JsonType = {
                command = "Follow";
                username = username;
                password = "";
                following = following;
                tweet = "";
                tweetId = -1;
                hashtag = ""
            }
            let serializedJson = Json.serialize json
            server.Send serializedJson

        | Tweet(tweet) ->
            let json : JsonType = {
                command = "Tweet";
                username = username;
                password = "";
                following = "";
                tweet = tweet;
                tweetId = -1;
                hashtag = ""
            }
            let serializedJson = Json.serialize json
            server.Send serializedJson

        | Retweet(tweetId) ->
            let json : JsonType = {
                command = "Retweet";
                username = username;
                password = "";
                following = "";
                tweet = "";
                tweetId = tweetId;
                hashtag = ""
            }
            let serializedJson = Json.serialize json
            server.Send serializedJson

        | QueryTweetsSubscribedTo ->
            let json : JsonType = {
                command = "QueryTweetsSubscribedTo";
                username = username;
                password = "";
                following = "";
                tweet = "";
                tweetId = -1;
                hashtag = ""
            }
            let serializedJson = Json.serialize json
            server.Send serializedJson

        | QueryTweetsWithHashtag(hashtag) ->
            let json : JsonType = {
                command = "QueryTweetsWithHashtag";
                username = username;
                password = "";
                following = "";
                tweet = "";
                tweetId = -1;
                hashtag = hashtag
            }
            let serializedJson = Json.serialize json
            server.Send serializedJson

        | QueryTweetsWithMentions ->
            let json : JsonType = {
                command = "QueryTweetsWithMentions";
                username = username;
                password = "";
                following = "";
                tweet = "";
                tweetId = -1;
                hashtag = ""
            }
            let serializedJson = Json.serialize json
            server.Send serializedJson 
        
        return! loop()
    }
    loop ()


let rec readUserInput (clientRef : Akka.Actor.IActorRef) =
    printfn "[Client] Enter command:"
    let userInput = Console.ReadLine()
    let userInputList = userInput.Split "|"
    let command = userInputList.[0]
    match (command) with
    | "Register" ->
        let username = userInputList.[1]
        let password = userInputList.[2]
        clientRef <! Register(username, password)
        
    | "Login" ->
        let username = userInputList.[1]
        let password = userInputList.[2]
        clientRef <! Login(username, password)

    | "Logout" ->
        clientRef <! Logout

    | "Follow" ->
        let following = userInputList.[1]
        clientRef <! Follow(following)

    | "Tweet" ->
        let tweet = userInputList.[1]
        clientRef <! Tweet(tweet)

    | "Retweet" ->
        let tweetId = int (userInputList.[1])
        clientRef <! Retweet(tweetId)
    
    | "QueryTweetsSubscribedTo" ->
        clientRef <! QueryTweetsSubscribedTo

    | "QueryTweetsWithHashtag" ->
        let hashtag = userInputList.[1]
        clientRef <! QueryTweetsWithHashtag(hashtag)

    | "QueryTweetsWithMentions" ->
        clientRef <! QueryTweetsWithMentions
    
    | _ -> ()
    
    readUserInput clientRef


[<EntryPoint>]
let main argv =
    // Connect to the server using websocket
    let server = new WebSocket("ws://localhost:8080/websocket")
    server.OnOpen.Add(fun args -> printfn "[Client] Opened WebSocket...")
    server.OnClose.Add(fun args -> printfn "[Client] Closed WebSocket...")
    server.OnMessage.Add(fun args -> printfn "[Client] Message: %A" args.Data)
    server.OnError.Add(fun args -> printfn "[Client] Error: %A" args.Message)
    server.Connect()

    // Initialize system and create client actor
    let system = System.create "client" (Configuration.load())
    let clientRef = spawn system "clientActor" (clientActor server)

    // Start the client process
    readUserInput clientRef

    // Wait indefinitely
    system.Terminate() |> ignore
    0