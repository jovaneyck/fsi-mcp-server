// F# Interactive Scratch File

let countChar (char: char) (str: string) =
    str.ToCharArray()
    |> Array.filter (fun c -> c = char)
    |> Array.length

let word = "strawberry"
let rCount = countChar 'r' word
