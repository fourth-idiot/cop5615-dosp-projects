# [COP5615] DOSP Project 2

## Team members
- Nikhil Mukesh Saoji (UFID: 93162869)
- Srikanth Rao Parcha (UFID: 46324560)

## Steps to run the program
1. Move to the project directory
2. Run the following command: `donet run <numNodes> <topology> <algorithm>`

## Gossip algorithm
In the gossip protocol, our aim is to propagate message(s) across all the nodes in a network. Our gossip implementation has two main components, the counter and the topology. The counter keeps track of all the nodes that have converged.
It also terminates the whole system whenever all the nodes converge. The topology describes the arrangement of all the nodes in the system. In our implementation, whenever a node receives a message for the first time(Initialization), we are starting a scheduler to invoke that actor periodically. At every round, the actor selects a random neighbor and spreads the message until either the whole system is converged or when a particular node is terminated.
We have set up the convergence and termination condition as following:
 - Convergence condition: The node is said to converged when it receives the message atleast once. The entire system converges when all the nodes recieve the message at least once.
 - Termination condition: Upon convergence, a node keeps transmitting a messgae until it has received that message upto nine times. Keeping an actor alieve after its convergence aids in achieving the convergence of the whole system. Siimultaneously, the termination condition helps in efficiently managing the network overhead caused by the actor space. It is imperative that the more the number of nodes, the more is the overhead on the network.

#### What is working?
All the topologies have successfully converged various number of nodes

#### The largest network we managed to deal with for each type of topology is as follows:

| Topology | Largest network size |
|----------|-----------------|
| Line          | 15000     |
| 2D            | 15000     |
| Imperfect 2D  | 15000     |
| 3D            | 15000     |
| Imperfect 3D  | 15000     |
| Full          | 15000     |

## PushSum algorithm
Push sum aims to calculate the sum/average of the all the nodes values in a network. Our push sum algorithm that calculate the average is implemented in the following manner. In the first round, it sends the pair (si, wi) to itself and starts a scheduler. Here, si is the node value xi and wi is initialized to 1 for all the nodes. At every subsequent time step t, si is updated to the summation of all the messages it has received in the time step t-1. Similar process is followed to calculate wi values. Now, a neighbor is selected randomly. Then, the si and wi values are halved. Subsequently, a half is sent to the neighbor and the other half is sent to itself. At a given round, every actor also checks for its s/w value. Whenever the difference between the past 10 (count) consecutive S/W values is less than 10^-10, the actor converges and stops participating in the subsequent rounds. We tried with the different count values ranging from 3 to 15. At 3, nodes converged quickly but the accuracy was low. However at 15, the accuracy was high, but the time taken by a node to converge was also high. Hence, we selected 10 out count value.

#### What is working?
All the topologies have successfully converged various number of nodes

#### The largest network we managed to deal with for each type of topology is as follows:

| Topology | Largest network size |
|----------|-----------------|
| Line          | 3000      |
| 3D            | 8000      |
| Imperfect 3D  | 8000      |
| Full          | 8000      |
