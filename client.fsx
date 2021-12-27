#r "nuget: Akka.FSharp" 
#r "nuget: Akka.TestKit" 
#r "nuget: Akka.Remote"
#r "nuget: Akka.Serialization.Hyperion"
open Akka.FSharp
open Akka.Actor
open System.Net
open System.Text
open System.Security.Cryptography
open System

let config =
    Configuration.parse
        @"akka {
            actor.provider = ""Akka.Remote.RemoteActorRefProvider, Akka.Remote""
            remote.helios.tcp {
                hostname = ""127.0.0.1""
                port = 9002
            }
        }"

let chars = Array.concat([[|'a' .. 'z'|]])
let size = Array.length chars - 1 
let ufID = "vkhilariwal"

let trailZeros = 5
let workers = 8

let zeros = String(Array.create trailZeros '0')
let ip = Environment.GetCommandLineArgs().[2]
let remoteSystem = create "BitCoinGeneratorRemote" config
let serverRef = remoteSystem.ActorSelection ("akka.tcp://BitCoinGenerator@" + ip + ":9001/user/supervisorNode")

let processorNode (mailbox: Actor<_>) = 
    
    let rec loop () = actor {

        let ranStr n = 
            let r = Random()
            let chars = Array.concat([[|'a' .. 'z'|];[|'A' .. 'Z'|];[|'0' .. '9'|]])
            let sz = Array.length chars in
            ufID+String(Array.init n (fun _ -> chars.[r.Next sz]))

        let byteToHex bytes = 
            bytes 
            |> Array.map (fun (x : byte) -> System.String.Format("{0:X2}", x))
            |> String.concat System.String.Empty

        let hash (coin: string) =
            let sha256Hash = Security.Cryptography.SHA256Managed.Create()
            coin
            |> System.Text.Encoding.ASCII.GetBytes
            |> sha256Hash.ComputeHash 
            |> byteToHex

        let verifyCoin coin trailZeros zeros =
            let bitcoin = hash coin
            if (String.Compare(bitcoin.[0..trailZeros-1], zeros) = 0)
            then bitcoin
            else "falseCoin"

        let processBatch sender (batch:int) = 
            for i in 0..batch do
                let text = ranStr 5
                let response = verifyCoin (text) trailZeros zeros
                match response with
                |   "falseCoin" -> ()
                |   _ -> sender <! "Client -> " + text + " " + response


        let! message = mailbox.Receive ()
        let sender = mailbox.Sender()
        processBatch sender (int message)
        
        sender <! "Complete"
        return! loop ()
    }
    serverRef <! "Complete"
    loop ()

let actors = [|
    for i in 1..workers do
        spawn remoteSystem ("remoteprocessorNode" + string i) processorNode
    |]

Console.ReadLine() |> ignore