open System
open Deedle
open Akka.FSharp


///////////////////////
// Utility functions //
///////////////////////

// Round number of nodes to get perfect square in case of 2D and imperfect 2D grid
let roundNodes numNodes topology =
    match topology with
    | "2d"
    | "imperfect2d" -> Math.Pow (Math.Round (sqrt (float numNodes)), 2.0) |> int
    | "3d"
    | "imperfect3d" -> Math.Pow (Math.Round ((float numNodes) ** (1.0 / 3.0)), 3.0)  |> int
    | _ -> numNodes
// roundNodes 15 "3d"

// Select random element from a list
let pickRandom (l: List<_>) =
    let r = Random()
    l.[r.Next(l.Length)]
// pickRandom([1; 2; 3; 4; 5; 6; 7; 8; 9; 10])

let getRandomNeighborID (topologyMap: Map<_, _>) nodeID =
    let (neighborList: List<_>) = (topologyMap.TryFind nodeID).Value
    let random = Random()
    neighborList.[random.Next(neighborList.Length)]


//////////////////////////
// Different topologies //
//////////////////////////

let buildLineTopology numNodes =
    let mutable map = Map.empty
    [ 1 .. numNodes ]
    |> List.map (fun nodeID ->
        let listNeighbors = List.filter (fun y -> (y = nodeID + 1 || y = nodeID - 1)) [ 1 .. numNodes ]
        map <- map.Add(nodeID, listNeighbors))
    |> ignore
    map

// Find neighbors of any particular node in a 2D grid
let gridNeighbors2D nodeID numNodes =
    let mutable map = Map.empty
    let lenSide = sqrt (float numNodes) |> int
    [ 1 .. numNodes ]
    |> List.filter (fun y ->
        if (nodeID % lenSide = 0) then (y = nodeID - 1 || y = nodeID - lenSide || y = nodeID + lenSide)
        elif (nodeID % lenSide = 1) then (y = nodeID + 1 || y = nodeID - lenSide || y = nodeID + lenSide)
        else (y = nodeID - 1 || y = nodeID + 1 || y = nodeID - lenSide || y = nodeID + lenSide))
// gridNeighbors2D 5 9

let build2DTopology numNodes =
    let mutable map = Map.empty
    [ 1 .. numNodes ]
    |> List.map (fun nodeID ->
        let listNeighbors = gridNeighbors2D nodeID numNodes
        map <- map.Add(nodeID, listNeighbors))
    |> ignore
    map

let buildImperfect2DTopology numNodes =
    let mutable map = Map.empty
    [ 1 .. numNodes ]
    |> List.map (fun nodeID ->
        let mutable listNeighbors = gridNeighbors2D nodeID numNodes
        let random =
            [ 1 .. numNodes ]
            |> List.filter (fun m -> m <> nodeID && not (listNeighbors |> List.contains m))
            |> pickRandom
        let listNeighbors = random :: listNeighbors
        map <- map.Add(nodeID, listNeighbors))
    |> ignore
    map

// Find neighbors of any particular node in a 3D grid
let gridNeighbors3D nodeID numNodes =
    let lenSide = Math.Round(Math.Pow((float numNodes), (1.0 / 3.0))) |> int
    [ 1 .. numNodes ]
    |> List.filter (fun y ->
        if (nodeID % lenSide = 0) then
            if (nodeID % (int (float (lenSide) ** 2.0)) = 0) then
                (y = nodeID - 1 || y = nodeID - lenSide || y = nodeID - int ((float (lenSide) ** 2.0)) || y = nodeID + int ((float (lenSide) ** 2.0)))
            elif (nodeID % (int (float (lenSide) ** 2.0)) = lenSide) then
                (y = nodeID - 1 || y = nodeID + lenSide || y = nodeID - int ((float (lenSide) ** 2.0)) || y = nodeID + int ((float (lenSide) ** 2.0)))
            else
                (y = nodeID - 1 || y = nodeID - lenSide || y = nodeID + lenSide || y = nodeID - int ((float (lenSide) ** 2.0)) || y = nodeID + int ((float (lenSide) ** 2.0)))        
        elif (nodeID % lenSide = 1) then
            if (nodeID % (int (float (lenSide) ** 2.0)) = 1) then
                (y = nodeID + 1 || y = nodeID + lenSide || y = nodeID - int ((float (lenSide) ** 2.0)) || y = nodeID + int ((float (lenSide) ** 2.0)))
            elif (nodeID % (int (float (lenSide) ** 2.0)) = int (float (lenSide) ** 2.0) - lenSide + 1 ) then
                (y = nodeID + 1 || y = nodeID - lenSide || y = nodeID - int ((float (lenSide) ** 2.0)) || y = nodeID + int ((float (lenSide) ** 2.0)))
            else
                (y = nodeID + 1 || y = nodeID - lenSide || y = nodeID + lenSide || y = nodeID - int ((float (lenSide) ** 2.0)) || y = nodeID + int ((float (lenSide) ** 2.0)))
        elif (nodeID % (int (float (lenSide) ** 2.0)) > 1) && (nodeID % (int (float (lenSide) ** 2.0)) < lenSide) then
            (y = nodeID - 1 || y = nodeID + 1 || y = nodeID + lenSide || y = nodeID - int ((float (lenSide) ** 2.0)) || y = nodeID + int ((float (lenSide) ** 2.0)))
        elif (nodeID % (int (float (lenSide) ** 2.0)) > int (float (lenSide) ** 2.0) - lenSide + 1) && (nodeID % (int (float (lenSide) ** 2.0)) < (int (float (lenSide) ** 2.0))) then
            (y = nodeID - 1 || y = nodeID + 1 || y = nodeID - lenSide || y = nodeID - int ((float (lenSide) ** 2.0)) || y = nodeID + int ((float (lenSide) ** 2.0)))
        else
            (y = nodeID - 1 || y = nodeID + 1 || y = nodeID - lenSide || y = nodeID + lenSide || y = nodeID - int ((float (lenSide) ** 2.0)) || y = nodeID + int ((float (lenSide) ** 2.0))))
// gridNeighbors3D 5 9

let build3DTopology numNodes =
    let mutable map = Map.empty
    [ 1 .. numNodes ]
    |> List.map (fun nodeID ->
        let listNeighbors = gridNeighbors3D nodeID numNodes
        map <- map.Add(nodeID, listNeighbors))
    |> ignore
    map

let buildImperfect3DTopology numNodes =
    let mutable map = Map.empty
    [ 1 .. numNodes ]
    |> List.map (fun nodeID ->
        let mutable listNeighbors = gridNeighbors3D nodeID numNodes
        let random =
            [ 1 .. numNodes ]
            |> List.filter (fun m -> m <> nodeID && not (listNeighbors |> List.contains m))
            |> pickRandom
        let listNeighbors = random :: listNeighbors
        map <- map.Add(nodeID, listNeighbors))
    |> ignore
    map

let buildFullTopology numNodes =
    let mutable map = Map.empty
    [ 1 .. numNodes ]
    |> List.map (fun nodeID ->
        let listNeighbors = List.filter (fun y -> nodeID <> y) [ 1 .. numNodes ]
        map <- map.Add(nodeID, listNeighbors))
    |> ignore
    map

let buildTopology numNodes topology =
    let mutable map = Map.empty
    match topology with
    | "line" -> buildLineTopology numNodes
    | "2d" -> build2DTopology numNodes
    | "imperfect2d" -> buildImperfect2DTopology numNodes
    | "3d" -> build3DTopology numNodes
    | "imperfect3d" -> buildImperfect3DTopology numNodes
    | "full" -> buildFullTopology numNodes

///////////////////
// Counter Actor //
///////////////////

type CounterMessage =
    | GossipNodeConverge
    | PushSumNodeConverge of int * float

type Result = { NumberOfNodesConverged: int; TimeElapsed: int64; }

let counter initialCount numNodes (filepath: string) (stopWatch: Diagnostics.Stopwatch) (mailbox: Actor<'a>) =
    let rec loop count (dataframeList: Result list) =
        actor {
            let! message = mailbox.Receive()
            match message with
            | GossipNodeConverge ->
                printfn "[INFO] Number of nodes converged: %d" (count + 1)
                let newRecord = { NumberOfNodesConverged = count + 1; TimeElapsed = stopWatch.ElapsedMilliseconds; }
                if (count + 1 = numNodes) then
                    stopWatch.Stop()
                    printfn "[INFO] Gossip Algorithm has converged in %d ms" stopWatch.ElapsedMilliseconds
                    let dataframe = Frame.ofRecords dataframeList
                    dataframe.SaveCsv(filepath, separator='\t')
                    mailbox.Context.System.Terminate() |> ignore
                return! loop (count + 1) (List.append dataframeList [newRecord])
            | PushSumNodeConverge (nodeID, avg) ->
                printfn "[INFO] Node %d has been converged s/w=%f)" nodeID avg
                let newRecord = { NumberOfNodesConverged = count + 1; TimeElapsed = stopWatch.ElapsedMilliseconds }
                if (count + 1 = numNodes) then
                    stopWatch.Stop()
                    printfn "[INFO] Push Sum Algorithm has converged in %d ms" stopWatch.ElapsedMilliseconds
                    let dataframe = Frame.ofRecords dataframeList
                    dataframe.SaveCsv(filepath, separator='\t')
                    mailbox.Context.System.Terminate() |> ignore
                return! loop (count + 1) (List.append dataframeList [newRecord])
        }
    loop initialCount []


//////////////////
// Gossip Actor //
//////////////////

let gossip maxCount (topologyMap: Map<_, _>) nodeID counterRef (mailbox: Actor<_>) = 
    let rec loop (count: int) = actor {
        let! message = mailbox.Receive ()
        // Handle message here
        match message with
        | "heardRumor" ->
            // If the heard rumor count is zero, tell the counter that it has heard the rumor and start spreading it.
            // Else, increment the heard rumor count by 1
            if count = 0 then
                mailbox.Context.System.Scheduler.ScheduleTellOnce(
                    TimeSpan.FromMilliseconds(25.0),
                    mailbox.Self,
                    "spreadRumor"
                )
                // printfn "[INFO] Node %d has been converged" nodeID
                counterRef <! GossipNodeConverge
                return! loop (count + 1)
            else
                return! loop (count + 1)
        | "spreadRumor" ->
            // Stop spreading the rumor if has an actor heard the rumor atleast 10 times
            // Else, Select a random neighbor and send message "heardRumor"
            // Start scheduler to wake up at next time step
            if count >= maxCount then
                return! loop count
            else
                let neighborID = getRandomNeighborID topologyMap nodeID
                let neighborPath = @"akka://my-system/user/worker" + string neighborID
                let neighborRef = mailbox.Context.ActorSelection(neighborPath)
                neighborRef <! "heardRumor"
                mailbox.Context.System.Scheduler.ScheduleTellOnce(
                    TimeSpan.FromMilliseconds(25.0),
                    mailbox.Self,
                    "spreadRumor"
                )
                return! loop count
        | _ ->
            printfn "[INFO] Node %d has received unhandled message" nodeID
            return! loop count
    }
    loop 0


//////////////
// Push sum //
//////////////
 
type PushSumMessage =
    | Initialize
    | Message of float * float
    | Round

let pushSum (topologyMap: Map<_, _>) nodeID counterRef (mailbox: Actor<_>) = 
    let rec loop sNode wNode sSum wSum count isTransmitting = actor {
        if isTransmitting then
            let! message = mailbox.Receive ()
            match message with
            | Initialize ->
                mailbox.Self <! Message (float nodeID, 1.0)
                mailbox.Context.System.Scheduler.ScheduleTellRepeatedly (
                    TimeSpan.FromMilliseconds(0.0),
                    TimeSpan.FromMilliseconds(25.0),
                    mailbox.Self,
                    Round
                )
                return! loop (float nodeID) 1.0 0.0 0.0 0 isTransmitting
            | Message (s, w) ->
                return! loop sNode wNode (sSum + s) (wSum + w) count isTransmitting
            | Round ->
                // Select a random neighbor and send (s/2, w/2) to it
                // Send (s/2, w/2) to itself
                let neighborID = getRandomNeighborID topologyMap nodeID
                let neighborPath = @"akka://my-system/user/worker" + string neighborID
                let neighborRef = mailbox.Context.ActorSelection(neighborPath)
                mailbox.Self <! Message (sSum / 2.0, wSum / 2.0)
                neighborRef <! Message (sSum / 2.0, wSum / 2.0)
                // Check convergence
                // Actor is said to converged if s/w did not change
                // more than 10^-10 for 3 consecutive rounds
                if(abs ((sSum / wSum) - (sNode / wNode)) < 1.0e-10) then
                    let newCount = count + 1
                    if newCount = 10 then
                        counterRef <! PushSumNodeConverge (nodeID, sSum / wSum)
                        return! loop sSum wSum 0.0 0.0 newCount false
                    else
                        return! loop (sSum / 2.0) (wSum / 2.0) 0.0 0.0 newCount isTransmitting 
                else
                    return! loop (sSum / 2.0) (wSum / 2.0) 0.0 0.0 0 isTransmitting
    }
    loop (float nodeID) 1.0 0.0 0.0 0 true


[<EntryPoint>]
let main argv =
    let system = System.create "my-system" (Configuration.load())

    // Number of times any single node should heard the rumor before stop transmitting it
    let maxCount = 10
    
    // Parse command line arguments
    let topology = argv.[1]
    let numNodes = roundNodes (int argv.[0]) topology
    let algorithm = argv.[2]
    let filepath = "results/" + topology + "-" + string numNodes + "-" + algorithm + ".csv"
    
    // Create topology
    let topologyMap = buildTopology numNodes topology

    // Initialize stopwatch
    let stopWatch = Diagnostics.Stopwatch()

    // Spawn the counter actor
    let counterRef = spawn system "counter" (counter 0 numNodes filepath stopWatch)

    // Run an algorithm based on user input
    match algorithm with
    | "gossip" ->
        // Gossip Algorithm
        // Create desired number of workers and randomly pick 1 to start the algorithm
        let workerRef =
            [ 1 .. numNodes ]
            |> List.map (fun nodeID ->
                let name = "worker" + string nodeID
                spawn system name (gossip maxCount topologyMap nodeID counterRef))
            |> pickRandom
        // Start the timer
        stopWatch.Start()
        // Send message
        workerRef <! "heardRumor"

    | "pushsum" ->
        // Push Sum Algorithm
        // Initialize all the actors
        let workerRef =
            [ 1 .. numNodes ]
            |> List.map (fun nodeID ->
                let name = "worker" + string nodeID
                (spawn system name (pushSum topologyMap nodeID counterRef)))
        // Start the timer
        stopWatch.Start()
        // Send message
        workerRef |> List.iter (fun item -> item <! Initialize)


    // Wait till all the actors are terminated
    system.WhenTerminated.Wait()
    0 // return an integer exit code
    // Each actor will have a flag to describe its active state