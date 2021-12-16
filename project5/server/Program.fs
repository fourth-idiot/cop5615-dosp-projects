open Akka.FSharp
open FSharp.Json
open Suave
open Suave.Filters
open Suave.Operators
open Suave.RequestErrors
open Suave.Sockets
open Suave.Sockets.Control
open Suave.WebSocket
open System
open System.Text.RegularExpressions


type RequestType = {
    command : string
    username : string
    password : string
    following : string
    tweet : string
    tweetId : int
    hashtag : string
}


type ResponseType = {
    status : string
    data : string
}


type TweetResponseType = {
    tweetFrom : string
    tweetId : int
    tweet : string
    isRetweet : Boolean
    author : string
}


type QueryResponseType = {
    status : string
    data : List<TweetResponseType>
    errorMessage : string
}


type userManagementMessages =
    | Register of (string * string * WebSocket)
    | Login of (string * string * WebSocket)
    | Logout of (string * WebSocket)
    | Follow of (string * string * WebSocket)
    | ValidateUser of (string)
    | ValidateUserForMentions of (string)
    | UsersToUpdateNewsFeedByTweetOrRetweet of (string)


type newsFeedMessages =
    | Tweet of (string * string)
    | Retweet of (string * int)
    | QueryTweetsSubscribedTo of (string)


type tweetParserMessages =
    | Parse of (int * string * string)
    | QueryTweetsWithHashtag of (string * string)
    | QueryTweetsWithMentions of (string)


let userManagementActor (mailbox: Actor<_>) =

    // Data to store
    let mutable users = Map.empty
    let mutable webSockets = Map.empty
    let mutable followers = Map.empty
    let mutable activeUsers = Set.empty

    let rec loop () = actor {

        let! message = mailbox.Receive ()
        let sender = mailbox.Sender ()

        // Initialize response
        let mutable response:ResponseType = {
            status = "";
            data = ""
        }

        // Handle message here
        match message with
        | Register(username, password, webSocket) ->
            if(username = "") then
                return! loop()
            elif(users.ContainsKey(username)) then
                printfn "[Server] User %s already exists." username
                response <- {
                    status = "Error";
                    data = sprintf "User %s already exists." username
                }
            else
                users <- Map.add username password users
                webSockets <- Map.add username webSocket webSockets
                followers <- Map.add username Set.empty followers
                activeUsers <- Set.add username activeUsers
                printfn "[Server] User %s registered successfully." username
                response <- {
                    status = "Success";
                    data = sprintf "User %s registered successfully." username
                }
            sender <? response |> ignore

        | Login(username, password, webSocket) ->
            if(activeUsers.Contains username) then
                printfn "[Server] User %s is already logged in." username
                response <- {
                    status = "Error";
                    data = sprintf "User %s is already logged in." username
                }
            elif(users.ContainsKey username) then
                if(password.Equals(users[username])) then
                    activeUsers <- Set.add username activeUsers
                    printfn "[Server] User %s logged in successfully." username
                    response <- {
                        status = "Success";
                        data = sprintf "User %s logged in successfully." username
                    }
                else
                    printfn "[Server] User %s entered wrong password. Please try again." username
                    response <- {
                        status = "Error";
                        data = sprintf "User %s entered wrong password. Please try again." username
                    }
            else
                response <- {
                    status = "Error";
                    data = sprintf "User %s is not registered." username
                }
            sender <? response |> ignore

        | Logout(username, webSocket) ->
            if(users.ContainsKey username) then
                if(Set.contains username activeUsers) then
                    activeUsers <- Set.remove username activeUsers
                    printfn "[Server] User %s logged out successfully." username
                    response <- {
                        status = "Success";
                        data = sprintf "User %s logged out successfully." username
                    }
                else
                    printfn "[Server] User %s is not logged in." username
                    response <- {
                        status = "Error";
                        data = sprintf "User %s is not logged in." username
                    }
            else
                response <- {
                    status = "Error";
                    data = sprintf "User %s is not registered." username
                }
            sender <? response |> ignore

        | Follow(follower, following, webSocket) ->
            if(not(users.ContainsKey follower)) then
                printfn "[Server] Follower user %s is not registered." follower
                response <- {
                    status = "Error";
                    data = sprintf "Follower user %s is not registered." follower
                }
            elif(not(followers.ContainsKey following)) then
                printfn "[Server] Following user %s is not registered." following
                response <- {
                    status = "Error";
                    data = sprintf "Following user %s is not registered." following
                }
            elif(not(activeUsers.Contains follower)) then
                printfn "[Follow] Follower user %s is not logged in." follower
                response <- {
                    status = "Error";
                    data = sprintf "Following user %s is not logged in." following
                }
            elif(follower = following) then
                printfn "[Server] Users cannot follow themselves."
                response <- {
                    status = "Error";
                    data = sprintf "Users cannot follow themselves."
                }
            elif(followers.[following].Contains follower) then
                printfn "[Server] User %s already follows user %s" follower following
                response <- {
                    status = "Error";
                    data = sprintf "User %s already followes user %s." follower following
                }
            else
                let mutable followingFollowers = followers.[following]
                followingFollowers <- Set.add follower followingFollowers
                followers <- Map.remove following followers
                followers <- Map.add following followingFollowers followers
                printfn "[Server] User %s started following user %s" follower following
                response <- {
                    status = "Success";
                    data = sprintf "User %s started following user %s" follower following
                }
            sender <? response |> ignore

        | ValidateUser(username) ->
            let mutable isValidUser = false
            let mutable message = ""
            if(not(users.ContainsKey username)) then
                message <- sprintf "User %s is not registered" username
            elif(not(activeUsers.Contains username)) then
                message <- sprintf "User %s is not logged in" username
            else
                isValidUser <- true
            sender <? (isValidUser, message) |> ignore

        | ValidateUserForMentions(username) ->
            let mutable isValidUser = false
            let mutable message = ""
            if(not(users.ContainsKey username)) then
                message <- sprintf "User %s is not registered" username
            else
                isValidUser <- true
            sender <? (isValidUser, message) |> ignore

        | UsersToUpdateNewsFeedByTweetOrRetweet(username) ->
            let allFollowers = followers.[username]
            let activeFollowers = Set.intersect allFollowers activeUsers
            let activeFollowers = webSockets |> Map.filter (fun username webSocket -> activeFollowers.Contains username)
            sender <? (allFollowers, activeFollowers) |> ignore

        return! loop ()
    }
    loop ()


let tweetParserActor userManagementRef (mailbox: Actor<_>) =

    // Data to store
    let mutable hashtagsTweets = Map.empty
    let mutable mentionsFeed = Map.empty

    let rec loop () = actor {

        let! message = mailbox.Receive ()
        let sender = mailbox.Sender ()
        
        // Handle message here
        match message with
        | Parse(tweetId, tweet, author) ->
            // Find hashtags
            let mutable (hashtags:Set<string>) = Set.empty;
            let hashtagRegex = new Regex(@"#\w+")
            let hashtagMatches = hashtagRegex.Matches(tweet)
            for hashtagMatch in hashtagMatches do
                let hashtag = hashtagMatch.Value
                hashtags <- Set.add hashtag hashtags
                if(hashtagsTweets.ContainsKey hashtag) then
                    let mutable hashtagTweets = hashtagsTweets[hashtag]
                    hashtagTweets <- List.append hashtagTweets [(tweetId, tweet, author)]
                    hashtagsTweets <- Map.remove hashtag hashtagsTweets
                    hashtagsTweets <- Map.add hashtag hashtagTweets hashtagsTweets
                else
                    printfn "[Server] Hashtag %s registered successfully" hashtag 
                    hashtagsTweets <- Map.add hashtag [(tweetId, tweet, author)] hashtagsTweets

            // Find mentions
            let mentionsRegex = new Regex(@"@\w+")
            let mentionsMatches = mentionsRegex.Matches(tweet)
            for mentionsMatch in mentionsMatches do
                let mentions = mentionsMatch.Value.Substring(1)
                let validateMentionsPromise = userManagementRef <? ValidateUserForMentions(mentions)
                let validateMentionsResponse : (Boolean * string) = Async.RunSynchronously (validateMentionsPromise, 10000)
                let isValidMention, errorMessage = validateMentionsResponse
                if(isValidMention) then
                    if(mentionsFeed.ContainsKey mentions) then
                        let mutable mentionsMentionsFeed = mentionsFeed.[mentions]
                        mentionsMentionsFeed <- List.append mentionsMentionsFeed [(tweetId, tweet, author)]
                        mentionsFeed <- Map.remove mentions mentionsFeed
                        mentionsFeed <- Map.add mentions mentionsMentionsFeed mentionsFeed
                    else
                        mentionsFeed <- Map.add mentions [(tweetId, tweet, author)] mentionsFeed

        | QueryTweetsWithHashtag(username, hashtag) ->
            let mutable response : QueryResponseType = {
                status = "";
                data = [];
                errorMessage = "";
            }
            let validateUserPromise = userManagementRef <? ValidateUser(username)
            let validateUserResponse : (Boolean * string) = Async.RunSynchronously (validateUserPromise)
            let isValidUser, errorMessage = validateUserResponse
            if(isValidUser) then
                if(hashtagsTweets.ContainsKey hashtag) then
                    printfn "[Server] User %s validated for querying tweets with hashtag %s" username hashtag
                    let mutable data = []
                    let tweets = hashtagsTweets.[hashtag]
                    for tweet in tweets do
                        let tweetId, tweetContent, tweetAuthor = tweet
                        let tweetObject : TweetResponseType = {
                                tweetFrom = tweetAuthor;
                                tweetId = tweetId;
                                tweet = tweetContent;
                                isRetweet = false;
                                author = tweetAuthor
                            }
                        data <- List.append data [tweetObject]
                    response <- {
                        status = "Success";
                        data = data;
                        errorMessage = ""
                    }
                else
                    printfn "[Server] No tweets with hashtag %s to query" hashtag
                    response <- {
                        status = "Success";
                        data = [];
                        errorMessage = ""
                    }
            else
                response <- {
                    status = "Error";
                    data = [];
                    errorMessage = errorMessage
                }
            sender <? response |> ignore

        | QueryTweetsWithMentions(username) ->
            let mutable response : QueryResponseType = {
                status = "";
                data = [];
                errorMessage = ""
            }
            // Validate user
            let validateUserPromise = userManagementRef <? ValidateUser(username)
            let validateUserResponse : (Boolean * string) = Async.RunSynchronously (validateUserPromise)
            let isValidUser, errorMessage = validateUserResponse
            if(isValidUser) then
                printfn "[Server] User %s validated for querying mentions." username
                if(mentionsFeed.ContainsKey username) then
                    let tweets = mentionsFeed.[username]
                    let mutable data = []
                    for tweet in tweets do
                        let tweetId, tweetContent, tweetAuthor = tweet
                        let tweetObject : TweetResponseType = {
                            tweetFrom = tweetAuthor;
                            tweetId = tweetId;
                            tweet = tweetContent;
                            isRetweet = false;
                            author = tweetAuthor
                        }
                        data <- List.append data [tweetObject]
                    response <- {
                        status = "Success";
                        data = data;
                        errorMessage = ""
                    }
                else
                    response <- {
                        status = "Success";
                        data = [];
                        errorMessage = ""
                    }
            else
                response <- {
                    status = "Error";
                    data = [];
                    errorMessage = errorMessage
                }
            sender <? response |> ignore
        
        return! loop ()
    }
    loop ()


let newsFeedActor userManagementRef tweetParserRef (mailbox : Actor<_>) =

    // Data to store
    let mutable (tweets: List<List<string>>) = []
    let mutable newsFeed = Map.empty

    let rec loop () = actor {
        
        let! message = mailbox.Receive ()
        let sender = mailbox.Sender ()

        // Handle message here
        match message with
        | Tweet(username, tweet) ->
            let mutable response : TweetResponseType = {
                tweetFrom = "";
                tweetId = -1;
                tweet = "";
                isRetweet = false;
                author = ""
            }
            // Validate user
            let validateUserPromise = userManagementRef <? ValidateUser(username)
            let validateUserResponse : (Boolean * string) = Async.RunSynchronously (validateUserPromise, 10000)
            let isValidUser, errorMessage = validateUserResponse
            if(isValidUser) then
                printfn "[Server] User %s validated for tweet" username
                let tweetId = tweets.Length
                tweets <- List.append tweets [[username; tweet]]
                printfn "[Server] User %s tweeted: %s" username tweet
                let usersToUpdateNewsFeedByTweetOrRetweetPromise = userManagementRef <? UsersToUpdateNewsFeedByTweetOrRetweet(username)
                let usersToUpdateNewsFeedByTweetOrRetweetResponse : (Set<string> * Map<string, WebSocket>) = Async.RunSynchronously (usersToUpdateNewsFeedByTweetOrRetweetPromise, 10000)
                let allFollowers, activeFollowers = usersToUpdateNewsFeedByTweetOrRetweetResponse
                for follower in allFollowers do
                    if(newsFeed.ContainsKey follower) then
                        let mutable followerNewsFeed = newsFeed.[follower]
                        followerNewsFeed <- List.append followerNewsFeed [(username, tweetId, false)]
                        newsFeed <- Map.remove follower newsFeed
                        newsFeed <- Map.add follower followerNewsFeed newsFeed
                    else
                        newsFeed <- Map.add follower [(username, tweetId, false)] newsFeed
                response <- {
                    tweetFrom = username
                    tweetId = tweetId;
                    tweet = tweet;
                    isRetweet = false;
                    author = username
                }
                sender <? (response, activeFollowers) |> ignore
                // Parse tweet
                tweetParserRef <! Parse(tweetId, tweet, username)

        | Retweet(username, tweetId) ->
            let mutable response : TweetResponseType = {
                tweetFrom = "";
                tweetId = -1;
                tweet = "";
                isRetweet = false;
                author = ""
            }
            // Validate user
            let validateUserPromise = userManagementRef <? ValidateUser(username)
            let validateUserResponse : (Boolean * string) = Async.RunSynchronously (validateUserPromise)
            let isValidUser, errorMessage = validateUserResponse
            if(isValidUser) then
                printfn "[Server] User %s validated for retweet." username
                let tweetObject = tweets.[tweetId]
                let author = tweetObject.[0]
                let tweet = tweetObject.[1]
                printfn "[Server] User %s retweeted tweet from user %s: %s" username author tweet
                let usersToUpdateNewsFeedByTweetOrRetweetPromise = userManagementRef <? UsersToUpdateNewsFeedByTweetOrRetweet(username)
                let usersToUpdateNewsFeedByTweetOrRetweetResponse : (Set<string> * Map<string, WebSocket>) = Async.RunSynchronously (usersToUpdateNewsFeedByTweetOrRetweetPromise, 10000)
                let allFollowers, activeFollowers = usersToUpdateNewsFeedByTweetOrRetweetResponse
                for follower in allFollowers do
                    if(newsFeed.ContainsKey follower) then
                        let mutable followerNewsFeed = newsFeed.[follower]
                        followerNewsFeed <- List.append followerNewsFeed [(username, tweetId, true)]
                        newsFeed <- Map.remove follower newsFeed
                        newsFeed <- Map.add follower followerNewsFeed newsFeed
                    else
                        newsFeed <- Map.add follower [(username, tweetId, true)] newsFeed
                response <- {
                    tweetFrom = username
                    tweetId = tweetId;
                    tweet = tweet;
                    isRetweet = true;
                    author = author
                }
                sender <? (response, activeFollowers) |> ignore

        | QueryTweetsSubscribedTo(username) ->
            let mutable response : QueryResponseType = {
                status = "";
                data = [];
                errorMessage = ""
            }
            // Validate user
            let validateUserPromise = userManagementRef <? ValidateUser(username)
            let validateUserResponse : (Boolean * string) = Async.RunSynchronously (validateUserPromise)
            let isValidUser, errorMessage = validateUserResponse
            if(isValidUser) then
                printfn "[Server] User %s validated for querying tweets subscribed to." username
                if(newsFeed.ContainsKey username) then
                    let entries = newsFeed.[username]
                    let mutable data = []
                    for entry in entries do
                        let tweetFrom, tweetId, isRetweet = entry
                        let tweetContent = tweets.[tweetId].[1]
                        let tweetAuthor = tweets.[tweetId].[0]
                        let tweetObject : TweetResponseType = {
                            tweetFrom = tweetFrom;
                            tweetId = tweetId;
                            tweet = tweetContent;
                            isRetweet = isRetweet;
                            author = tweetAuthor
                        }
                        data <- List.append data [tweetObject]
                    response <- {
                        status = "Success";
                        data = data;
                        errorMessage = ""
                    }
                else
                    response <- {
                        status = "Success";
                        data = [];
                        errorMessage = ""
                    }
            else
                response <- {
                    status = "Error";
                    data = [];
                    errorMessage = errorMessage
                }
            sender <? response |> ignore

        return! loop ()
    }
    loop ()


let ws userManagementRef tweetParserRef newsFeedRef (webSocket : WebSocket) (context : HttpContext) =
    socket {
        let mutable loop = true
        while loop do
            let! msg = webSocket.read()
            match msg with
            | (Text, data, true) ->
                // Parse request
                let serializedJson = UTF8.toString data
                let mutable json = Json.deserialize<RequestType> serializedJson
                let command = json.command
                let username = json.username
                let password = json.password
                let following = json.following
                let tweet = json.tweet
                let tweetId = json.tweetId
                let hashtag = json.hashtag

                // Initialize promise
                if(command = "Tweet") then
                    let promise = newsFeedRef <? Tweet(username, tweet)
                    let response : (TweetResponseType * Map<string, WebSocket>) = Async.RunSynchronously (promise, 10000)
                    let tweetResponse, activeFollowers = response
                    let byteResponse =
                        Json.serialize tweetResponse
                        |> System.Text.Encoding.ASCII.GetBytes
                        |> ByteSegment
                    for entry in activeFollowers do
                        do! entry.Value.send Text byteResponse true
                        
                elif(command = "Retweet") then
                    let promise = newsFeedRef <? Retweet(username, tweetId)
                    let response : (TweetResponseType * Map<string, WebSocket>) = Async.RunSynchronously (promise, 10000)
                    let retweetResponse, activeFollowers = response
                    let byteResponse =
                        Json.serialize retweetResponse
                        |> System.Text.Encoding.ASCII.GetBytes
                        |> ByteSegment
                    for entry in activeFollowers do
                        do! entry.Value.send Text byteResponse true

                elif((command = "Register") || (command = "Login") || (command = "Logout") || (command = "Follow")) then
                    let mutable promise = userManagementRef <? Register("", "", webSocket)
                    if command = "Register" then
                        promise <- userManagementRef <? Register(username, password, webSocket)
                    elif command = "Login" then
                        promise <- userManagementRef <? Login(username, password, webSocket)
                    elif command = "Logout" then
                        promise <- userManagementRef <? Logout(username, webSocket)
                    elif command = "Follow" then
                        promise <- userManagementRef <? Follow(username, following, webSocket)
                    let response : ResponseType = Async.RunSynchronously (promise, 10000)
                    let byteResponse =
                        Json.serialize response
                        |> System.Text.Encoding.ASCII.GetBytes
                        |> ByteSegment
                    do! webSocket.send Text byteResponse true

                else
                    let mutable promise = newsFeedRef <? QueryTweetsSubscribedTo("")
                    if command = "QueryTweetsSubscribedTo" then
                        promise <- newsFeedRef <? QueryTweetsSubscribedTo(username)
                    elif command = "QueryTweetsWithHashtag" then
                        promise <- tweetParserRef <? QueryTweetsWithHashtag(username, hashtag)
                    elif command = "QueryTweetsWithMentions" then
                        promise <- tweetParserRef <? QueryTweetsWithMentions(username)
                    // Wait until the promise is resolved
                    let response : QueryResponseType = Async.RunSynchronously (promise, 10000)                
                    let byteResponse =
                        Json.serialize response
                        |> System.Text.Encoding.ASCII.GetBytes
                        |> ByteSegment
                    do! webSocket.send Text byteResponse true
            
            | (Close, _, _) ->
                let emptyResponse = [||] |> ByteSegment
                do! webSocket.send Close emptyResponse true
                loop <- false

            | _ -> ()
    }


[<EntryPoint>]
let main _ =
    // Initialize system
    let system = System.create "client" (Configuration.load())
    let userManagementRef = spawn system "userManagementActor" userManagementActor
    let tweetParserRef = spawn system "tweetParserActor" (tweetParserActor userManagementRef)
    let newsFeedRef = spawn system "newsFeedActor" (newsFeedActor userManagementRef tweetParserRef)
    
    let app : WebPart =
        choose [
            path "/websocket" >=> handShake (ws userManagementRef tweetParserRef newsFeedRef)
            path "/websocketwithSubprotocol" >=> handShakeWithSubprotocol (chooseSubprotocol "test") (ws userManagementRef tweetParserRef newsFeedRef)
            NOT_FOUND "[Server] No handlers found..."
        ]
    startWebServer defaultConfig app
    0