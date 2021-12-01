// Learn more about F# at http://docs.microsoft.com/dotnet/fsharp

open System
open System.Threading
open Akka.FSharp
open Akka.Remote


type UserMessages =
    | Ready of (string * int * List<string> * Akka.Actor.ActorSelection)
    | Action
    | GoOnline
    | GoOffline

// User actor
let userActor (mailbox: Actor<_>) =
    
    let mutable username = ""
    let mutable isOnline = false
    let mutable numUsers = 0
    let mutable allHashtags = []
    let mutable serverRef = mailbox.Context.System.ActorSelection("")
    let mutable tweetCount = 0

    let rec loop () = actor {
        let! message = mailbox.Receive ()

        // Handle message here
        match message with
        | Ready(u, tu, ah, sRef) ->
            username <- u
            isOnline <- true
            allHashtags <- ah
            serverRef <- sRef
            mailbox.Context.System.Scheduler.ScheduleTellOnce(TimeSpan.FromMilliseconds(1000.0), mailbox.Self, Action)

        | Action ->
            if isOnline then
                let actions = [1; 2; 3; 4; 5; 6; 7]
                let action = actions.[(new Random()).Next(actions.Length)]
                match action with
                // 1 -> Tweet, 2 -> Tweet with only hashtag, 3 -> Tweet with both hashtag and mentions, 4 -> Retweet
                // 5 -> Query tweets subscribed to, 6. Query tweets with hashtag, 7. Query tweets with mentions
                | 1 ->
                    tweetCount <- tweetCount + 1
                    let tweet = sprintf "%s tweeted -> tweet_%d" username tweetCount
                    serverRef <! ("Tweet", username, tweet, "")
                | 2 ->
                    tweetCount <- tweetCount + 1
                    let hashtag = allHashtags.[(new Random()).Next(allHashtags.Length)]
                    let tweet = sprintf "%s tweeted -> tweet_%d with hashtag %s" username tweetCount hashtag
                    serverRef <! ("Tweet", username, tweet, "")
                | 3 ->
                    tweetCount <- tweetCount + 1
                    let hashtag = allHashtags.[(new Random()).Next(allHashtags.Length)]
                    let mentions = sprintf "user_%d" ((new Random()).Next(numUsers))
                    let tweet = sprintf "%s tweeted -> tweet_%d with hashtag %s and mentions @%s" username tweetCount hashtag mentions
                    serverRef <! ("Tweet", username, tweet, "")
                | 4 ->
                    printfn "[User] %s doing retweet" username
                    serverRef <! ("Retweet", username, "", "")
                | 5 ->
                    serverRef <! ("QueryTweetsSubscribedTo", username, "", "")
                | 6 ->
                    let hashtag = allHashtags.[(new Random()).Next(allHashtags.Length)]
                    serverRef <! ("QueryTweetsWithHashtag", hashtag, "", "")
                | 7 ->
                    serverRef <! ("QueryTweetsWithMentions", username, "", "")
                | _ ->
                    ignore()
                
                mailbox.Context.System.Scheduler.ScheduleTellOnce(TimeSpan.FromMilliseconds(1000.0), mailbox.Self, Action)
        
        | GoOnline ->
            isOnline <- true
            printfn "[User] %s logged in successfully. Getting newsfeed..." username
            mailbox.Context.System.Scheduler.ScheduleTellOnce(TimeSpan.FromMilliseconds(1000.0), mailbox.Self, Action)

        | GoOffline ->
            isOnline <- false
            printfn "[User] %s logged out successfully" username

        return! loop ()
        
    }
    loop ()

// Admin actor
let adminActor (mailbox: Actor<_>) =

    // Data to store
    let serverRef = mailbox.Context.System.ActorSelection(sprintf "akka.tcp://TwitterEngine@localhost:8080/user/serverActor")
    let mutable numUsers = 0
    let mutable allHastgas = [
        "#omicronishere"
        "#lockdown";
        "#covid19";
        "#chirstmas";
        "#emirates";
        "#thanksgiving";
        "#metoo";
        "#giveaway";
        "#metaverse";
        "#ootd";
        "#fomo";
        "#photography";
        "#gogators";
        "#messy";
        "#cr7";
        "#mondaymotiviation";
        "#winterishere";
        "#ILY3000";
        "#tbt";
        "#fun";
        "#contest";
        "#darthvader";
        "#nofilter";
        "#sunset";
        "#picoftheday";
        "#design";
        "#COP5615isgreat";
        "#iloveindia";
        "#longweekend";
        "#vegetarian";
    ]
    let mutable users = Set.empty
    let mutable addresses = Map.empty
    let mutable followers = Map.empty
    let mutable hashtags = Set.empty
    let mutable subscribers = Map.empty
    let mutable currentOffline = Set.empty
    
    let rec loop () = actor {
        
        let! (message:obj) = mailbox.Receive ()
        let ((messageType, _, _): Tuple<string, string, string>) = downcast message

        // Handle message here
        match messageType with
        | "StartSimulator" ->
            let ((_, strNumUsers, _): Tuple<string, string, string>) = downcast message
            numUsers <- strNumUsers |> int32
            printfn "[Admin] Starting simulator by registering at the server."
            serverRef <! ("RegisterSimulator", "", "", "")

        | "AcknowledgeSimulatorRegistration" ->
            printfn "[Admin] Simulator registered successfully. Starting user registration."
            mailbox.Self <! ("RegisterUser", "1", "")
            mailbox.Context.System.Scheduler.ScheduleTellRepeatedly(TimeSpan.FromMilliseconds(6000.0), TimeSpan.FromMilliseconds(5000.0), mailbox.Self, ("Follow", "", ""))
            mailbox.Context.System.Scheduler.ScheduleTellRepeatedly(TimeSpan.FromMilliseconds(6000.0), TimeSpan.FromMilliseconds(5000.0), mailbox.Self, ("Subscribe", "", ""))
            mailbox.Context.System.Scheduler.ScheduleTellRepeatedly(TimeSpan.FromMilliseconds(12000.0), TimeSpan.FromMilliseconds(10000.0), mailbox.Self, ("LoginLogout", "", ""))

        | "RegisterUser" ->
            let ((_, uid, _): Tuple<string, string, string>) = downcast message
            let numUid = uid |> int32
            let username = sprintf "user_%s" uid
            let userRef = spawn mailbox.Context.System username userActor
            addresses <- Map.add username userRef addresses
            serverRef <! ("RegisterUser", username, username, "")
            if(numUid < numUsers) then
                let nextNumUid = numUid + 1
                let nextStrUid  = nextNumUid |> string
                mailbox.Context.System.Scheduler.ScheduleTellOnce(TimeSpan.FromMilliseconds(1000.0), mailbox.Self, ("RegisterUser", nextStrUid, ""))

        | "AcknowledgeUserRegistration" ->
            let ((_, username, _): Tuple<string, string, string>) = downcast message
            printfn "[Admin] User %s registered successfully" username
            users <- Set.add username users
            followers <- Map.add username Set.empty followers
            addresses.[username] <! Ready(username, numUsers, allHastgas, serverRef)
        
        | "Follow" ->
            for following in users do
                let numUid = int (following.Split "_").[1]
                let remainingUsers = Set.difference users (Set.union followers.[following] (Set.empty.Add following))
                if (((followers.[following]).Count < ((numUsers - 1) / numUid)) && (remainingUsers.Count > 0)) then
                    let follower =
                        remainingUsers
                        |> Set.toList
                        |> (fun s -> s.[(new Random()).Next(s.Length)])
                    serverRef <! ("Follow", follower, following, "")

        | "AcknowledgeFollowRequest" ->
            let ((_, follower, following): Tuple<string, string, string>) = downcast message
            let mutable followingFollowers = followers.[following]
            followingFollowers <- Set.add follower followingFollowers
            followers <- Map.remove following followers
            followers <- Map.add following followingFollowers followers
            printfn "[Admin] %s started following %s" follower following

        | "Subscribe" ->
            serverRef <! ("GetRegisteredHashtags", "", "", "")
        
        | "SubscribeForward" ->
            let ((_, h, _): Tuple<string, string, string>) = downcast message
            let updatedHashtags = h.Split(",")

            for hashtag in updatedHashtags do
                if(not(hashtags.Contains hashtag)) then
                    hashtags <- Set.add hashtag hashtags
                    subscribers <- Map.add hashtag Set.empty subscribers
            for i in 1 .. allHastgas.Length do
                let hashtag = allHastgas.[i-1]
                if(hashtags.Contains hashtag) then
                    let remainingUsers = Set.difference users subscribers.[hashtag]
                    if (((subscribers.[hashtag]).Count < ((allHastgas.Length / i))) && (remainingUsers.Count > 0)) then
                        let subscriber =
                            remainingUsers
                            |> Set.toList
                            |> (fun s -> s.[(new Random()).Next(s.Length)])
                        serverRef <! ("Subscribe", subscriber, hashtag, "")

        | "AcknowledgeSubscribeRequest" ->
            let ((_, subscriber, hashtag): Tuple<string, string, string>) = downcast message
            let mutable hashtagSubscribers = subscribers.[hashtag]
            hashtagSubscribers <- Set.add subscriber hashtagSubscribers
            subscribers <- Map.remove hashtag subscribers
            subscribers <- Map.add hashtag hashtagSubscribers subscribers
            printfn "[Admin] %s subscribed to %s" subscriber hashtag

        | "LoginLogout" ->
            let total = users.Count
            let offlineTotal = (30 * total) / 100
            let mutable newOffline = Set.empty
            for i in [1..offlineTotal] do
                let mutable nextOffline =
                    users
                    |> Set.toList
                    |> (fun s -> s.[(new Random()).Next(s.Length)])
                while((currentOffline.Contains nextOffline) || (newOffline.Contains(nextOffline))) do
                    nextOffline <-
                        users
                        |> Set.toList
                        |> (fun s -> s.[(new Random()).Next(s.Length)])
                serverRef <! ("Logout", nextOffline, "", "")
                newOffline <- Set.add nextOffline newOffline
            for nextOnline in currentOffline do
                serverRef <! ("Login", nextOnline, nextOnline, "")

        | "AcknowledgeUserLogin" ->
            let ((_, username, _): Tuple<string, string, string>) = downcast message
            if(currentOffline.Contains username) then
                currentOffline <- Set.remove username currentOffline
                addresses.[username] <! GoOnline

        | "AcknowledgeUserLogout" ->
            let ((_, username, _): Tuple<string, string, string>) = downcast message
            if(not(currentOffline.Contains username)) then
                currentOffline <- Set.add username currentOffline
                addresses.[username] <! GoOffline

        return! loop ()
    }
    loop ()

[<EntryPoint>]
let main argv =
    // Client configurations
    let config =
        Configuration.parse
            @"akka {
                actor {
                    provider = ""Akka.Remote.RemoteActorRefProvider, Akka.Remote""
                }
                remote.helios.tcp {
    		            port = 8081
    		            hostname = localhost
                    }
                }
            }"
    let system = System.create "TwitterSimulator" config

    let adminRef = spawn system "adminActor" adminActor
    adminRef <! ("StartSimulator", "5", "")
    Thread.Sleep(600000)
    0 // return an integer exit code