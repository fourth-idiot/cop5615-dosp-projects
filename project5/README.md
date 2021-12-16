
# [COP5615] DOSP Project 4 part 2: Twitter Clone with WebSockets

## Demo video link [[Click Here](https://youtu.be/YHIj8-naEW4)]

## Team members
- Nikhil Mukesh Saoji (UFID: 9316 2869)
- Srikanth Rao Parcha (UFID: 4632 4560)

## Functionalities of the server/engine implemented
1) User registration
2) Login/Logout
3) Follow
4) Tweet with hashtag/mentions
5) Retweet
6) Query tweets subscribed to
7) Query tweets with hashtag
8) Query tweets with mentions (My mention)

## Server/Engine side
1) The server deals with the requests from all the clients in the system.
2) The server makes use of Suave framework to communicate with the clients as it provides the WebSocket interface.
3) All the communications that were previously done via Akka.Remote are now done by Suave WebSocket.
4) Suave WebSocket handles request and response object between the client and the server in JSON format.

## Client side
1) On the client side, desired action is captured from command line. It is a string where each component in the desired action is separated using `|`.
2) WebSocketSharp is used to provide WebScoket protocol on the client side.

## Steps to run the program
#### On server side:
1. Move to the engine directory `cd sever`
2. Run the following command: `dotnet run`
#### On client side(for every client/user):
1. Move to the simulator directory `cd client`
2. Run the following command: `dotnet run`
