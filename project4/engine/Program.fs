// Learn more about F# at http://docs.microsoft.com/dotnet/fsharp

#if INTERACTIVE
#r "nuget: Akka.FSharp"
#r "nuget: Akka.Remote"
#endif

open System
open System.IO;
open System.Threading
open System.Text.RegularExpressions
open Akka.FSharp


type perfStatsMessages =
    | PrintPerfStats of (string * string)

type userManagementMessages =
    | Register of (string * string * string)
    | Login of (string * string * string)
    | Logout of (string * string)
    | Follow of (string * string * string)
    | Subscribe of (string * string * string)
    | ValidateUserForTweet of (string * string)
    | ValidateUserForRetweet of (string * int)
    | ValidateUserForMentions of (string * string)
    | UsersToUpdateNewsFeedByTweet of (string * string)
    | UsersToUpdateNewsFeedByRetweet of (string * string)
    | UsersToUpdateNewsFeedByTweetWithHashtag of (string * string)
    | ValidateUserForQueryTweetsSubscribedTo of (string)
    | ValidateUserForQueryTweetsWithMentions of (string)
    | GetRegisteredHashtags of (string)
    | CalculatePerfStats

type newsFeedActor =
    | CreateFeed of (string)
    | UpdateNewsFeedByTweet of (string * string)
    | UpdateNewsFeedByTweetForward of (Set<string> * Set<string> * string)
    | UpdateNewsFeedByRetweet of (string * string)
    | UpdateNewsFeedByRetweetForward of (Set<string> * Set<string> * string)
    | UpdateNewsFeedByTweetWithHashtag of (Set<string> * Set<string> * string)
    | QueryTweetsSubscribedTo of (string)
    | QueryTweetsSubscribedToForward of (string * bool)

type mentionsActor =
    | UpdateMentionsFeed of (string * bool * string)
    | QuerytweetsWithMentions of (string)
    | QuerytweetsWithMentionsForward of (string * bool)

type tweetParserMessages =
    | Parse of (string)
    | ParseForwardForUser of (string * bool * bool * string)
    | ParseForwardForHashtag of (Set<string> * Set<string> * string)
    | QueryTweetsWithHashtag of (string)

type tweetManagementMessages =
    | Tweet of (string * string)
    | TweetForward of (string * string * bool * string)
    | RandomRetweet of (string)
    | Retweet of (string * int)
    | RetweetForward of (string * int * bool * string)


let perfStatsActor perfStatsPath (mailbox: Actor<_>) =
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        // Handle message here
        match message with
        | PrintPerfStats(service, averageTime) ->
            File.AppendAllText(perfStatsPath, (sprintf "%s = %s" service averageTime))
        return! loop ()
    }
    loop ()


// User management actor
let userManagementActor perfStatsRef (mailbox: Actor<_>) =

    // Data to store
    let mutable requests = 0.0
    let mutable totalTime = 0.0
    let mutable users = Map.empty
    let mutable followers = Map.empty
    let mutable (subscribers:Map<string, Set<string>>) = Map.empty
    let mutable activeUsers = Set.empty

    let rec loop () = actor {

        let! message = mailbox.Receive ()
        let sender = mailbox.Sender ()

        // Handle message here
        match message with
        | Register(username, password, clientPath) ->
            users <- Map.add username password users
            followers <- Map.add username Set.empty followers
            activeUsers <- Set.add username activeUsers
            printfn "[Register] User %s registered successfully." username
            // Send acknowledgement to the client admin
            sender <! ("AcknowledgeUserRegistration", username, clientPath, "")

        | Login(username, password, clientPath) ->
            if(users.ContainsKey username) then
                if(password.Equals(users[username])) then
                    activeUsers <- Set.add username activeUsers
                    printfn "[Login] User %s logged in successfully" username
                    // Send acknowledgement to the client admin
                    sender <! ("AcknowledgeUserLogin", username, clientPath, "")
                else
                    printfn "[Login] User %s entered wrong password. Please try again." username
            else
                printfn "[Login] User %s is not registered." username

        | Logout(username, clientPath) ->
            if(users.ContainsKey username) then
                if(Set.contains username activeUsers) then
                    activeUsers <- Set.remove username activeUsers
                    printfn "[Logout] User %s logged out successfully." username
                    // Send acknowledgement to the client admin
                    sender <! ("AcknowledgeUserLogout", username, clientPath, "")
                else
                    printfn "[Logout] User %s is not logged in." username
            else
                printfn "[Logout] User %s is not registered." username

        | Follow(follower, following, clientPath) ->
            if(not(users.ContainsKey follower)) then
                printfn "[Follow] Follower user %s is not registered." follower
            elif(not(followers.ContainsKey following)) then
                printfn "[Follow] Following user %s is not registered." following
            elif(not(activeUsers.Contains follower)) then
                printfn "[Follow] Follower user %s is not logged in" follower
            elif(follower = following) then
                printfn "[Follow] Users cannot follow themselves"
            elif(followers.[following].Contains follower) then
                printfn "[Follow] User %s already follows user %s" follower following
            else
                let mutable followingFollowers = followers.[following]
                followingFollowers <- Set.add follower followingFollowers
                followers <- Map.remove following followers
                followers <- Map.add following followingFollowers followers
                printfn "[Follow] User %s started following user %s" follower following
                // Send acknowledgement to the client admin
                sender <! ("AcknowledgeFollowRequest", follower, following, clientPath)

        | Subscribe(subscriber, hashtag, clientPath) ->
            requests <- requests + 1.0
            if(not(users.ContainsKey subscriber)) then
                printfn "[Follow] Follower %s is not registered." subscriber
            elif(not(subscribers.ContainsKey hashtag)) then
                printfn "[Follow] Subscribing hashtag %s is not registered." hashtag
            elif(not(activeUsers.Contains subscriber)) then
                printfn "[Follow] Follower user %s is not logged in" subscriber
            elif(subscribers.[hashtag].Contains subscriber) then
                printfn "[Follow] User %s already follows hashtag %s" subscriber hashtag
            else
                let mutable hashtagSubscribers = subscribers.[hashtag]
                hashtagSubscribers <- Set.add subscriber hashtagSubscribers
                subscribers <- Map.remove hashtag subscribers
                subscribers <- Map.add hashtag hashtagSubscribers subscribers
                printfn "[Follow] User %s started following %s" subscriber hashtag
                // Send acknowledgement to the client admin
                sender <! ("AcknowledgeSubscribeRequest", subscriber, hashtag, clientPath)

        | ValidateUserForTweet(username, tweet) ->
            if(not(users.ContainsKey username)) then
                let status = false
                let errorMessage = "[Tweet] User " + username + " is not registered"
                sender <! TweetForward(username, tweet, status, errorMessage)
            elif(not(activeUsers.Contains username)) then
                let status = false
                let errorMessage = "[Tweet] User " + username + " is not logged in"
                sender <! TweetForward(username, tweet, status, errorMessage)
            else
                let status = true
                let errorMessage = ""
                sender <! TweetForward(username, tweet, status, errorMessage)

        | ValidateUserForRetweet(username, tweetId) ->
            if(not(users.ContainsKey username)) then
                let status = false
                let errorMessage = "[Retweet] User " + username + " is not registered"
                sender <! RetweetForward(username, tweetId, status, errorMessage)
            elif(not(activeUsers.Contains username)) then
                let status = false
                let errorMessage = "[Retweet] User " + username + " is not logged in"
                sender <! RetweetForward(username, tweetId, status, errorMessage)
            else
                let status = true
                let errorMessage = ""
                sender <! RetweetForward(username, tweetId, status, errorMessage)

        | ValidateUserForMentions(mentions, tweet) ->
            let mutable isRegistered = false
            let mutable isActive = false
            if(users.ContainsKey mentions) then
                isRegistered <- true
                if(activeUsers.Contains mentions) then
                    isActive <- true
            sender <! ParseForwardForUser(mentions, isRegistered, isActive, tweet)

        | UsersToUpdateNewsFeedByTweet(username, tweet) ->
            let allFollowers = followers.[username]
            let activeFollowers = Set.intersect allFollowers activeUsers
            sender <! UpdateNewsFeedByTweetForward(allFollowers, activeFollowers, tweet)
        
        | UsersToUpdateNewsFeedByRetweet(username, tweet) ->
            let allFollowers = followers.[username]
            let activeFollowers = Set.intersect allFollowers activeUsers
            sender <! UpdateNewsFeedByRetweetForward(allFollowers, activeFollowers, tweet)

        | UsersToUpdateNewsFeedByTweetWithHashtag(hashtag, tweet) ->
            if(not(subscribers.ContainsKey hashtag )) then
                subscribers <- Map.add hashtag Set.empty subscribers
                printfn "[Tweet] New hashtag from tweet %s in %s registered successfully" tweet hashtag
            let allSubscribers = subscribers.[hashtag]
            let activeFollowers = Set.intersect allSubscribers activeUsers
            sender <! ParseForwardForHashtag(allSubscribers, activeFollowers, tweet)

        | ValidateUserForQueryTweetsSubscribedTo(username) ->
            let mutable status = false
            if((users.ContainsKey username) && (activeUsers.Contains username)) then
                status <- true
            sender <! QueryTweetsSubscribedToForward(username, status)

        | ValidateUserForQueryTweetsWithMentions(mentions) ->
            let mutable status = false
            if((users.ContainsKey mentions) && (activeUsers.Contains mentions)) then
                status <- true
            sender <! QuerytweetsWithMentionsForward(mentions, status)

        | GetRegisteredHashtags(clientPath) ->
            sender <! ("GetRegistertedHashtagsForward", String.Join(",", subscribers.Keys), clientPath, "")

        return! loop ()
    }
    loop ()


// News feed
let newsFeedActor userManagementRef (mailbox: Actor<_>) =

    // Data to store
    let mutable newsFeed = Map.empty

    let rec loop () = actor {
        let! message = mailbox.Receive ()
        // Handle message here
        match message with
        | CreateFeed(username) ->
            newsFeed <- Map.add username List.empty newsFeed
            printfn "News feed for %s created successfully" username

        | UpdateNewsFeedByTweet(username, tweet) ->
            userManagementRef <! UsersToUpdateNewsFeedByTweet(username, tweet)

        | UpdateNewsFeedByTweetForward(allFollowers, activeFollowers, tweet) ->
            for follower in allFollowers do
                // Update news feed
                if(newsFeed.ContainsKey follower) then
                    let mutable followerNewsFeed = newsFeed.[follower]
                    followerNewsFeed <- List.append followerNewsFeed [tweet]
                    if(followerNewsFeed.Length > 100) then
                        followerNewsFeed <- followerNewsFeed[1..]
                    newsFeed <- Map.remove follower newsFeed
                    newsFeed <- Map.add follower followerNewsFeed newsFeed
                else
                    newsFeed <- Map.add follower [tweet] newsFeed
                if(activeFollowers.Contains follower) then
                    printfn "[News feed][%s] %s" follower tweet 

        | UpdateNewsFeedByRetweet(username, tweet) ->
            userManagementRef <! UsersToUpdateNewsFeedByRetweet(username, tweet)

        | UpdateNewsFeedByRetweetForward(allFollowers, activeFollowers, tweet) ->
            for follower in allFollowers do
                // Update news feed
                if(newsFeed.ContainsKey follower) then
                    let mutable followerNewsFeed = newsFeed.[follower]
                    followerNewsFeed <- List.append followerNewsFeed [tweet]
                    if(followerNewsFeed.Length > 100) then
                        followerNewsFeed <- followerNewsFeed[1..]
                    newsFeed <- Map.remove follower newsFeed
                    newsFeed <- Map.add follower followerNewsFeed newsFeed
                else
                    newsFeed <- Map.add follower [tweet] newsFeed
                if(activeFollowers.Contains follower) then
                    printfn "[News feed][%s] %s" follower tweet

        | UpdateNewsFeedByTweetWithHashtag(allSubscribers, activeSubscribers, tweet) ->
            for subscriber in allSubscribers do
                // Update news feed
                if(newsFeed.ContainsKey subscriber) then
                    let mutable followerNewsFeed = newsFeed.[subscriber]
                    followerNewsFeed <- List.append followerNewsFeed [tweet]
                    if(followerNewsFeed.Length > 100) then
                        followerNewsFeed <- followerNewsFeed[1..]
                    newsFeed <- Map.remove subscriber newsFeed
                    newsFeed <- Map.add subscriber followerNewsFeed newsFeed
                else
                    newsFeed <- Map.add subscriber [tweet] newsFeed
                if(activeSubscribers.Contains subscriber) then
                    printfn "[News feed][%s] %s" subscriber tweet

        | QueryTweetsSubscribedTo(username) ->
            // Validate user
            userManagementRef <! ValidateUserForQueryTweetsSubscribedTo(username)

        | QueryTweetsSubscribedToForward(username, status) ->
            if(status) then
                if(newsFeed.ContainsKey username) then
                    printfn "[News feed] News feed of user %s: %A" username newsFeed.[username]
                else
                    printfn "[News feed] News feed of user %s is empty" username
            else
                printfn "[News feed] User %s is either not registered or not active" username

        return! loop ()
    }
    loop ()


let mentionsFeedActor userManagementRef (mailbox: Actor<_>) =

    // Data to store
    let mutable mentionsFeed = Map.empty

    let rec loop () = actor {
        let! message = mailbox.Receive ()
        // Handle message here
        match message with
        | UpdateMentionsFeed(mentions, isActive, tweet) ->
            // Update mentions feed
            if(mentionsFeed.ContainsKey mentions) then
                let mutable mentionsMentionsFeed = mentionsFeed.[mentions]
                mentionsMentionsFeed <- List.append mentionsMentionsFeed [tweet]
                if(mentionsMentionsFeed.Length > 100) then
                        mentionsMentionsFeed <- mentionsMentionsFeed[1..]
                mentionsFeed <- Map.remove mentions mentionsFeed
                mentionsFeed <- Map.add mentions mentionsMentionsFeed mentionsFeed
            else
                mentionsFeed <- Map.add mentions [tweet] mentionsFeed
            if(isActive) then
                printfn "[Mentions][%s] %s" mentions tweet

        | QuerytweetsWithMentions(mentions) ->
            // Validate user
            userManagementRef <! ValidateUserForQueryTweetsWithMentions(mentions)

        | QuerytweetsWithMentionsForward(mentions, status) ->
            if(status) then
                if(mentionsFeed.ContainsKey mentions) then
                    printfn "[Mentions] Mentions of user %s: %A" mentions mentionsFeed.[mentions]
                else
                    printfn "[Mentions] User %s has no mentions" mentions
            else
                printfn "[Mentions] User %s is either not registered or not active" mentions
        
        return! loop ()
    }
    loop ()


let tweetParserActor userManagementRef newsFeedRef mentionsRef (mailbox: Actor<_>) =

    // Data to store
    let mutable hashtagsTweets = Map.empty

    let rec loop () = actor {
        let! message = mailbox.Receive ()
        
        // Handle message here
        match message with
        | Parse(tweet) ->
            // Find hashtags
            let mutable (hashtags:Set<string>) = Set.empty;
            let hashtagRegex = new Regex(@"#\w+")
            let hashtagMatches = hashtagRegex.Matches(tweet)
            for hashtagMatch in hashtagMatches do
                let hashtag = hashtagMatch.Value
                hashtags <- Set.add hashtag hashtags
                if(hashtagsTweets.ContainsKey hashtag) then
                    let mutable hashtagTweets = hashtagsTweets[hashtag]
                    hashtagTweets <- List.append hashtagTweets [tweet]
                    if(hashtagTweets.Length > 100) then
                        hashtagTweets <- hashtagTweets[1..]
                    hashtagsTweets <- Map.remove hashtag hashtagsTweets
                    hashtagsTweets <- Map.add hashtag hashtagTweets hashtagsTweets
                else
                    hashtagsTweets <- Map.add hashtag [tweet] hashtagsTweets
                // Validate hashtag
                userManagementRef <! UsersToUpdateNewsFeedByTweetWithHashtag(hashtag, tweet)

            // Find mentions
            let mentionsRegex = new Regex(@"@\w+")
            let mentionsMatches = mentionsRegex.Matches(tweet)
            for mentionsMatch in mentionsMatches do
                let mentions = mentionsMatch.Value.Substring(1)
                // Validate mentions
                userManagementRef <! ValidateUserForMentions(mentions, tweet)

        | ParseForwardForUser(mentions, isRegistered, isActive, tweet) ->
            if(isRegistered) then
                mentionsRef <! UpdateMentionsFeed(mentions, isActive, tweet)
                
        | ParseForwardForHashtag(allSubscribers, activeSubscribers, tweet) ->
            newsFeedRef <! UpdateNewsFeedByTweetWithHashtag(allSubscribers, activeSubscribers, tweet)

        | QueryTweetsWithHashtag(hashtag) ->
            if(hashtagsTweets.ContainsKey hashtag) then
                printfn "[Hashtags] Tweet with hashtag %s: %A" hashtag hashtagsTweets.[hashtag]
            else
                printfn "[Hashtags] No tweets with hashtags %s" hashtag
            
        return! loop ()
    }
    loop ()


// Tweet handler
let tweetManagementActor userManagementRef newsFeedRef tweetParserRef (mailbox: Actor<_>) =

    // Data to store
    let mutable (tweets: List<List<string>>) = []

    let rec loop () = actor {
        let! message = mailbox.Receive ()
        
        // Handle message here
        match message with
        | Tweet(username, tweet) ->
            // Validate user
            userManagementRef <! ValidateUserForTweet(username, tweet)

        | TweetForward(username, tweet, status, errorMessage) ->
            if(status) then
                tweets <- List.append tweets [[username; tweet]]
                newsFeedRef <! UpdateNewsFeedByTweet(username, tweet)
                // Send tweet to the tweet parser actor
                tweetParserRef <! Parse(tweet)
            else
                printfn "[Tweet] Could not validate user %s due to %s" username errorMessage

        | RandomRetweet(username) ->
            if(tweets.Length > 0) then
                let tweetId = (new Random()).Next(tweets.Length)
                mailbox.Self <! Retweet(username, tweetId)

        | Retweet(username, tweetId) ->
            // Validate user
            userManagementRef <! ValidateUserForRetweet(username, tweetId)

        | RetweetForward(username, tweetId, status, errorMessage) ->
            // Process the tweet further if user is validated
            if(status) then
                printfn "[Retweet] User %s validated" username
                // Process the tweet further if tweet exists
                if(tweetId < tweets.Length) then
                    let tweet = tweets.[tweetId].[1]
                    newsFeedRef <! UpdateNewsFeedByRetweet(username, tweet)
                else
                    printfn "[Retweet] Tweet does not exist"
            else
                printfn "[Retweet] Could not validate user %s due to %s" username errorMessage

        return! loop ()
    }
    loop ()


let serverActor userManagementRef newsFeedRef mentionsRef tweetParserRef tweetManagementRef perfStatsPath (mailbox: Actor<_>) =

    // Data to store
    let mutable requests = 0.0
    let mutable startTime = DateTime.Now

    let rec loop () = actor {

        let! (message:obj) = mailbox.Receive ()
        let sender = mailbox.Sender ()

        let ((messageType, _, _, _): Tuple<string, string, string, string>) = downcast message
        // Handle message here
        match messageType with
        | "Start" ->
            File.AppendAllText(perfStatsPath, (sprintf "requests, totalTime, averageRequestsPerSecond\n"))
            mailbox.Context.System.Scheduler.ScheduleTellOnce(TimeSpan.FromMilliseconds(5000.0), mailbox.Self, ("PrintPerfStats", "", "", ""))

        | "RegisterSimulator" ->
            printfn "[Server] Registering simulator."
            sender <! ("AcknowledgeSimulatorRegistration", "", "")
            startTime <- DateTime.Now

        | "RegisterUser" ->
            let ((_, username, password, _): Tuple<string, string, string, string>) = downcast message
            let clientPath = sprintf "%A" sender.Path
            userManagementRef <! Register(username, password, clientPath)
            requests <- requests + 1.0

        | "AcknowledgeUserRegistration" ->
            let ((_, username, clientPath, _): Tuple<string, string, string, string>) = downcast message
            let clientRef = mailbox.Context.System.ActorSelection(clientPath)
            clientRef <! ("AcknowledgeUserRegistration", username, "")

        | "Login" ->
            let ((_, username, password, _): Tuple<string, string, string, string>) = downcast message
            let clientPath = sprintf "%A" sender.Path
            userManagementRef <! Login(username, password, clientPath)
            requests <- requests + 1.0

        | "AcknowledgeUserLogin" ->
            let ((_, username, clientPath, _): Tuple<string, string, string, string>) = downcast message
            newsFeedRef <! QueryTweetsSubscribedTo(username)
            let clientRef = mailbox.Context.System.ActorSelection(clientPath)
            clientRef <! ("AcknowledgeUserLogin", username, "")

        | "Logout" ->
            let ((_, username, _, _): Tuple<string, string, string, string>) = downcast message
            let clientPath = sprintf "%A" sender.Path
            userManagementRef <! Logout(username, clientPath)
            requests <- requests + 1.0

        | "AcknowledgeUserLogout" ->
            let ((_, username, clientPath, _): Tuple<string, string, string, string>) = downcast message
            let clientRef = mailbox.Context.System.ActorSelection(clientPath)
            clientRef <! ("AcknowledgeUserLogout", username, "")

        | "Follow" ->
            let ((_, follower, following, _): Tuple<string, string, string, string>) = downcast message
            let clientPath = sprintf "%A" sender.Path
            userManagementRef <! Follow(follower, following, clientPath)
            requests <- requests + 1.0

        | "AcknowledgeFollowRequest" ->
            let ((_, follower, following, clientPath): Tuple<string, string, string, string>) = downcast message
            let clientRef = mailbox.Context.System.ActorSelection(clientPath)
            clientRef <! ("AcknowledgeFollowRequest", follower, following)

        | "GetRegisteredHashtags" ->
            let clientPath = sprintf "%A" sender.Path
            userManagementRef <! GetRegisteredHashtags(clientPath)

        | "GetRegistertedHashtagsForward" ->
            let ((_, hashtags, clientPath, _): Tuple<string, string, string, string>) = downcast message
            let clientRef = mailbox.Context.System.ActorSelection(clientPath)
            clientRef <! ("SubscribeForward", hashtags, "")

        | "Subscribe" ->
            let ((_, subscriber, hashtag, _): Tuple<string, string, string, string>) = downcast message
            let clientPath = sprintf "%A" sender.Path
            userManagementRef <! Subscribe(subscriber, hashtag, clientPath)
            requests <- requests + 1.0

        | "AcknowledgeSubscribeRequest" ->
            let ((_, follower, hashtag, clientPath): Tuple<string, string, string, string>) = downcast message
            let clientRef = mailbox.Context.System.ActorSelection(clientPath)
            clientRef <! ("AcknowledgeSubscribeRequest", follower, hashtag)

        | "Tweet" ->
            let ((_, username, tweet, _): Tuple<string, string, string, string>) = downcast message
            tweetManagementRef <! Tweet(username, tweet)
            requests <- requests + 1.0

        | "Retweet" ->
            let ((_, username, _, _): Tuple<string, string, string, string>) = downcast message
            tweetManagementRef <! RandomRetweet(username)
            requests <- requests + 1.0

        | "QueryTweetsSubscribedTo" ->
            let ((_, username, _, _): Tuple<string, string, string, string>) = downcast message
            newsFeedRef <! QueryTweetsSubscribedTo(username)
            requests <- requests + 1.0

        | "QueryTweetsWithHashtag" ->
            let ((_, hashtag, _, _): Tuple<string, string, string, string>) = downcast message
            tweetParserRef <! QueryTweetsWithHashtag(hashtag)
            requests <- requests + 1.0

        | "QueryTweetsWithMentions" ->
            let ((_, mentions, _, _): Tuple<string, string, string, string>) = downcast message
            mentionsRef <! QuerytweetsWithMentions(mentions)
            requests <- requests + 1.0

        | "PrintPerfStats" ->
            let timeDiff = (DateTime.Now - startTime).TotalSeconds |> float
            if requests > 0.0 then
                let averageRequestsPerSecond = requests / timeDiff
                File.AppendAllText(perfStatsPath, (sprintf "%f, %f, %f\n" (Math.Round requests) (Math.Round (timeDiff, 2)) (Math.Round (averageRequestsPerSecond, 2))))
            mailbox.Context.System.Scheduler.ScheduleTellOnce(TimeSpan.FromMilliseconds(5000.0), mailbox.Self, ("PrintPerfStats", "", "", ""))

        | _ ->
            ignore()

        return! loop ()
    }
    loop ()


[<EntryPoint>]
let main argv =
    // Configuration
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
    let system = System.create "TwitterEngine" config

    let perfStatsPath = "perfStats.txt"

    // Create all services
    let perfStatsRef = spawn system "perStatsActor" (perfStatsActor perfStatsPath)
    let userManagementRef = spawn system "userManagementActor" (userManagementActor perfStatsRef)
    let newsFeedRef = spawn system "newsFeedActor" (newsFeedActor userManagementRef)
    let mentionsRef = spawn system "mentionsActor" (mentionsFeedActor userManagementRef)
    let tweetParserRef = spawn system "tweetParserActor" (tweetParserActor userManagementRef newsFeedRef mentionsRef)
    let tweetManagementRef = spawn system "tweetManagementActor" (tweetManagementActor userManagementRef newsFeedRef tweetParserRef)
    let serverRef = spawn system "serverActor" (serverActor userManagementRef newsFeedRef mentionsRef tweetParserRef tweetManagementRef perfStatsPath)

    serverRef <! ("Start", "", "", "")

    Thread.Sleep(6000000)
    0 // return an integer exit code