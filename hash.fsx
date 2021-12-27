#time "on"
#r "nuget: Akka.FSharp" 
#r "nuget: Akka.TestKit" 
open Akka.FSharp
open Akka.Actor
open System.Security.Cryptography
open System


let system = create "BitCoinGenerator" (Configuration.defaultConfig())
let chars = Array.concat([[|'a' .. 'z'|]])
let size = Array.length chars - 1 
let ufID = "vkhilariwal"
// let texts = seq{
//     for i in 0..size do
//         for a in 0..size do
//             for b in 0..size do
//                 for c in 0..size do
//                     for d in 0..size do
//                         ufID + String([|chars.[i];chars.[a];chars.[b];chars.[c];chars.[d];|])
//     }

// Console.WriteLine("Enter Trailing Zeros")
let trailZeros = 5
let workers = 4

let zeros = String(Array.create trailZeros '0')
let range = 100000;




type SupervisorMsg =
    | Start of int
    | Print of String
    | Complete of String

type WorkerMsg = 
    | Batch of String


let processorNode (mailbox: Actor<_>) = 
    
    let rec loop () = actor {

        // let texts =
        //     seq{
        //         for i in 0..size do
        //             for a in 0..size do
        //                 for b in 0..size do
        //                     for c in 0..size do
        //                         for d in 0..size do
        //                             ufID + String([|chars.[i];chars.[a];chars.[b];chars.[c];chars.[d];|])
        //     }

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

        let processBatch sender (batch:string[]) = 
            // let textBatch = texts |> Seq.skip (int batch.[0]) |> Seq.truncate (int batch.[1])
            // printfn "%A" textBatch
            // for text in texts.[int batch.[0]..int batch.[1]] do
            for i in 0..range do
                let text = ranStr 5
                let response = verifyCoin (text) trailZeros zeros
                match response with
                |   "falseCoin" -> ()
                |   _ -> sender <! Print (text + " " + response)


        let! message = mailbox.Receive ()
        let sender = mailbox.Sender()
        match message with
        | Batch batch -> batch.Split[|' '|] |> processBatch sender
        
        sender <! Complete (string message)

        return! loop ()
    }
    loop ()

  

let supervisorNode (mailbox: Actor<_>) = 
    let mutable start = 0
    let mutable count = 0
    let mutable actors = [|
            for i in 1..workers do
                spawn mailbox ("processorNode" + string i) processorNode
    |]

    let rec loop () = actor {

        let printBitcoin bitcoin =
            printfn "%s" bitcoin
            count <- count + 1

        let starter = 
            for actor in actors do
                actor <! Batch (string start + " " + string (start+range))
                start <- start + range

        let next sender =
            sender <! Batch (string start + " " + string (start+range))
            start <- start + range

        let! message = mailbox.Receive ()
        let sender = mailbox.Sender()

        match message with
        | Start workers -> starter
        | Print text -> printBitcoin text
        | Complete complete ->  next sender
        
        if count < 5
        then return! loop ()
        else exit 0
    }
    loop ()
let supervisorNodeRef = spawn system "supervisorNode" supervisorNode
supervisorNodeRef <! Start workers

Console.ReadLine() |> ignore
