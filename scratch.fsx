// F# Interactive Scratch File

let word = "strawberry"

let countChar (char : char) (str : string) : int = 
    str 
    |> Seq.filter (fun c -> c = char) 
    |> Seq.length

let countRs = countChar 'r' word