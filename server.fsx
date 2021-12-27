#time "on"
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
                port = 9001
            }
        }"

let system = create "BitCoinGenerator" config
let ufID = "vkhilariwal"

let trailZeros = Environment.GetCommandLineArgs().[2]
let workers = 10

let zeros = String(Array.create (int trailZeros) '0')
let range = 100000;

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
                let text = ranStr 7
                let response = verifyCoin (text) (int trailZeros) zeros
                match response with
                |   "falseCoin" -> ()
                |   _ -> sender <! text + " " + response


        let! message = mailbox.Receive ()
        let sender = mailbox.Sender()

        processBatch sender (int message)
        
        sender <! "Complete"

        return! loop ()
    }
    loop ()

let supervisorNode (mailbox: Actor<_>) = 
    let mutable count = 0

    let rec loop () = actor {

        let printBitcoin bitcoin =
            printfn "%s" bitcoin
            count <- count + 1            

        let! message = mailbox.Receive ()
        let sender = mailbox.Sender()

        let msg = string message
        match msg with
           | "Complete" -> 
                sender <! range
           | _ -> printBitcoin msg
            
        
        if count < 50
        then return! loop ()
        else exit 0
    }

    let actors = [|
        for i in 1..workers do
            spawn mailbox ("processorNode" + string i) processorNode
    |]
    for actor in actors do
        actor <! range
    loop ()


let supervisorNodeRef = spawn system "supervisorNode" supervisorNode

Console.ReadLine() |> ignore