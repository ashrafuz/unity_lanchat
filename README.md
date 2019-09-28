### Introduction
Hello! This is a study on LAN in Unity using only raw c# sockets. I was studying it because of a game I am currently working on. Thanks to my company Ulka Games (Subsidy of Moonfrog) to allow me to publish the study publicly.

## What to expect
Application wise: The application works between devices (even if one of them is editor), that are connected to the same wi-fi. By default, it connects 2 players. You can increase the number in lobby.scene with PLAYER_MAX_COUNT

Development wise: you should have a basic understanding of UDP & TCP. Also C# Reflection.

# Documentation

Well, the project is divided into two parts mainly. 
 1. Network Discovery : using UDP connection to discover other devices running the same app (done via PORT).
 2. Chat: After discovering the connections, we check if all connections (Defined by PLAYER_MAX_COUNT) joined in the network. If so, we connect the TCP clients together. And the rest of the chat is easily done via the TCP handler.
 
 ## Network Discovery
Its a UDP connection system. So the idea is, in a LAN, the first 3 section of the IP address is same. So lets say, device A & device B is connected to the same internet. Then, in a IPv4 system, they will share an IP like this :

* 123.456.0.XX [Example IP of Device A]

* 123.456.0.YY [Example IP of Device B]

You may know, the IP range in a common LAN system is 0-255. The address ~255 is important here. This address is known as Broadcast address of LAN. No device can use this address as its own IP, Moreover, every device in that LAN pool can listen to this address if they want.

So going back to our example, lets say device-A & B wants to connect, and device-A will be the host.

In this case, device-A will "ping" its own IP address (123.456.0.XX) to 123.456.0.255 (boradcast address) with a PORT_NUMBER and device-B will listen from 123.456.0.255:PORT_NUMBER

Essentially, our target is to create TCP client between A & B. To do that, both devices need to know each other's unique address (XX, YY). Thats why we need the broadcasting to let the devices discover each other. 

### So in subsequent steps:

1. Device-A Starts to broadcast its own address to 255:PORT_NUMBER channel. [Known as creating a room]

   a. As Device-A is the host, It creates a TCP server and joins itself as first client. [This was the most trickiest part]
   
2. Device-B starts to listen to .255:PORT_NUMBER channel for any incoming message
3. Device-B finds a message & after deserializing the message, we get the IP address of Device A (XX)
4. Device-B creates a TCP client using the server IP (XX) it got from broadcasting message. [Known as Joining a room]

5. Device-A accepts the incoming TCP client request and establishes connection.

## Chat
Well the rest of the section is easy. Once we have the connection between all clients, we just use TCP clients to talk to each other. To make our (devs) life easy, I used C# reflection to execute command on other clients. See LobbyManager.cs => ProcessMethod function.

* Please note that only HOST can send commands to other clients, the clients only can request for certain actions. Thats how I designed it, you may do your requirements as ncecessary.


# Tips
If one of your connection is Unity Editor, then you can follow the debug logs to see how the steps are done. Also, I have put some documentation where I could in the scripts, look at that if necessary.

## Room for Improvements
There is a lot of room for improvements in this project. I didn't have enough time to organize everything. Please bear in mind that its a very simple example. However, these are areas that need major attention:

* Acknowledgement Signals
* Host Migration
* Security Encoding
* Routing
* Broader Reflection Usage [Between different classes]
* Disconnection Handler
* UI improvements
* Code Organization


## Final words
Please feel free to criticize/enhance the project as you like. I did this project as fast as I could to meet deadline of my work.


LICENCE: Do whatever you want with it! Happy Coding!
