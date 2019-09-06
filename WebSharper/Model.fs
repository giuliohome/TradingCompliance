namespace ClientServerTable

open Import2TSS
open WebSharper
open SqlLib
open SqlDB


module ServerModel =


    type Permission =
        static member OilManual = "OilManual"
        static member OilReadETS = "OilReadETS"
        static member OilWriteETS = "OilWriteETS"



    let AppDB = "TSS_DB"
    

    
    [<JavaScript>]
    let EtsSpA = "ETS SpA"
    [<JavaScript>]
    let EtsInc = "ETS Inc"

    let getBookCo bookCo =
        match bookCo with
        | str when str = EtsSpA -> Import2TSS.Counterparty.ETS
        | str when str = EtsInc -> Import2TSS.Counterparty.Inc
        | _ -> ""


  