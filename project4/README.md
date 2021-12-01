# [COP5615] DOSP Project 4 part 1: Twitter clone and simulator 

## Team members
- Nikhil Mukesh Saoji (UFID: 9316 2869)
- Srikanth Rao Parcha (UFID: 4632 4560)

## Twitter clone:
The Twitter clone and simulator consists of two parts, the server and the simulator. The server deals with the functionality of the project, whereas the simulator deals with the utilization and testing of functionalities. 


## Functionalities of the server/engine implemented and their working-
1) Register- when a new user wants to register, if that user doesn’t already exist, they are added to a hash table 'users' containing their username, password, and followers list, which is initially empty.

2) Login- when a user wants to log in, the user's username and password are checked against the corresponding values in the hashtable. If they match, the user is logged in. And later, this user is added to a set called activeUsers that contains all the active users.

3) Logout- When the user wants to log out, the user's details are removed from the activeUsers set.

4)  Follow- When a user wants to follow another user, the follower is added to the followers list of the user followed in the 'followers' table.

5) Subscribe to hashtags- When a user subscribes to a hashtag, they receive every tweet made with the subscribed hashtag. This process is done by storing all the subscribers of a particular hashtag in a hashtable called the hashtagSubscribers. And all the tweets that fit the criteria are displayed on the user's news feed. 

6) Tweet- When a user wants to tweet, the tweet is displayed on the news feed of all the user's followers. The tweetManagementMessages actor does this process, and this actor also stores them.

7) Retweet- When a user wants to retweet, the tweet is displayed on the news feed of all the user's followers. The tweetManagementMessages actor displays tweets on all the necessary newsfeeds, and this actor also stores them.

8) Mentions- When the tweetParserMessages identify a tweet with a mention, the parsed mention, i.e., the user is notified of the mention on their respective news feed. 

9) Query subscribed tweets- Here, the tweets that are tweeted or retweeted by the users that this particular user follows and the hashtags that the user has subscribed to are returned to the user's newsfeed.

10) Query with hashtags- Here, all the tweets with that hashtags are displayed on the user's newsfeed.

11) Query with mentions- Here, all the mentions of a particular user are displayed on the user's newsfeed.


## Main actors in the server
1) userManagementActor-  This actor is concerned with registering, logging in, logging out, enabling the user to follow another user, and subscribing to hashtag(s).
2) newsFeedActor- This actor stores a news feed for every user. In other words, all the tweets associated with a user (can be retweeted tweets, tweets of the user that this particular user follows, etc.)
3) mentionsActor- every user has their own mention feed, and all the mentions of that user are maintained in their mentions feed.
4) tweetParserActor- this actor extracts any mentions or hashtags in every tweet.
5) tweetManagementActor- this actor manages all the tweets in the system. 



## Simulator/tester/client

### Architecture-
1) On the client side, every user is a new actor in the system.
2) The client-side has a scheduler that deals with controlling the actions of every user.
3) Every user/client can tweet, tweet with a single hashtag, tweet with both hashtags, retweet, query tweets subscribed to, query tweets with the hashtag, query tweets with mentions.
4) The scheduler periodically and randomly chooses one of the actions as mentioned earlier and performs them.
5) To simulate users' logging and logging out as organically as possible, for every user after a fixed inter of time, 30% of users randomly log off, and a different randomly selected who weren't logged in users log in. 


### Zipf distribution in following users-
1) The Zipf distribution is used to simulate the way users follow each other on Twitter.
2) Here, the first user in the 'users' table is followed by all the users apart from themselves so, if the number of users is 'n,' then 'n-1' users follow user one.
3) Similarly, for the second user, (n-1)/2 users follow the user, and for the third, (n-1)/3 users follow the third user, and so on for all the users in the 'users' table.


### Zipf distribution in hashtag subscription-
1) The Zipf distribution is used to simulate the way users subscribe to a hashtag on Twitter.
2)  Here, the first hashtag is subscribed to by all the users. So, if the number of users is 'n,' then 'n' users are subscribed to the first hashtag.
3) Similarly, for the second hashtag, n/2 users subscribe to the hashtag, and for the third, n/3 users subscribe to the third hashtag, and so on for all the hashtags.



## Results

| Number of users | Average number of request per second(upto 100,000 requests) |
|----------|-----------------|
| 500        | 458     |
| 1000       | 537     |
| 2000       | 594     |
| 3000       | 625     |
| 4000       | 656     |
| 5000       | 678     |
| 10000      | 809     |

## Steps to run the program
#### On server side:
1. Move to the engine directory `cd engine`
2. Run the following command: `dotnet run`
#### On client side:
1. Move to the simulator directory `cd simulator`
2. Run the following command: `dotnet run <numberOfUsers>`
