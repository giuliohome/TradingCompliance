namespace ClientServerTable

open WebSharper
open System
open System.Configuration
open System.Web
open System.Collections.Generic
open Import2TSS
open Newtonsoft.Json
open SqlLib
open SqlDB

module Server =
    
    [<JavaScript>]
    type EndPoint =
            | [<EndPoint "/">] Home
            | [<EndPoint "/about">] About
            | [<EndPoint "/OilData">] Table 
            | [<EndPoint "/OilAlerts">] Alerts
            | [<EndPoint "/OilAlerts.aspx">] OldAlerts
            | [<EndPoint "/OilAdmin">] Admin
            | [<EndPoint "/OilLog">] Log
            | [<EndPoint "/OilPivot">] Pivot
            | [<EndPoint "/Download">] Download
    

    [<JavaScript>]
    type JTableData = {bookcompany: string; user: string; code: string; status: string; key: string; closed: bool; dateFrom: string; dateTo: string; }

    [<JavaScript>]
    type JTableParams = { jtStartIndex: int; jtPageSize: int; jtSorting: string}

    //[<JavaScript>]
    //type JTableAlert = {AlertCode : string ; AlertKey : string ; AssignedTo : string ; BookCompany : string ; Commodity : string ; CreationDate : DateTime ; LastTransactionDate : Nullable<DateTime> ; Message : string ; Month : string ; Note : string ; Outcome : string ; Portfolio : string ; Status : string }

    
    [<JavaScript>]
    type JTableResult = {Message : string ; Records : string[] ; Result : string ; TotalRecordCount : int} //{|AlertCode : string ; AlertKey : string |}
    
    let SessionBookCo = "BookCo"

    let CheckAuthentication() = 
        if (HttpContext.Current.Request.Headers.Get("SMUSERNAME") <> null && HttpContext.Current.Request.Headers.["SMUSERNAME"].Trim() <> "") then
            string HttpContext.Current.Request.Headers.["SMUSERNAME"]
        else 
            ""
    
    let GetAuthUsername() = 
        if (HttpContext.Current.Session.["ReportingApp_UserSession"] = null) then
            HttpContext.Current.Session.["ReportingApp_UserSession"] <- CheckAuthentication()
        string HttpContext.Current.Session.["ReportingApp_UserSession"]

    let GetCurrentUser() = 
        match ConfigurationManager.AppSettings.["Mode"] with
        | confmode when confmode = " - MyHome" ->"EN21165"
        | confmode when confmode = " - Local" -> Environment.UserName.ToUpper()
        | _ -> GetAuthUsername()

    // from IE datepicker to F#: you tried Globalization, ParseExact, etc...  without luck
    // and this does the trick ;-)
    let cleanDate (str:string) : string =
        new string (str.ToCharArray()
        |> Array.filter(fun c -> int c < 128))
    // used for example in
    //let dateFrom = DateTime.Parse(cleanDate dateFromStr) 

    [<Rpc>]
    let GetOilAlertsAsync 
            (bookcompany: string, user: string, code: string , status: string, key: string, closed: bool, dateFrom: string, dateTo: string,
                 jtStartIndex: int, jtPageSize: int, jtSorting: string ) : Async<JTableResult> = 
            async {
                try 
                    let dateFromStr = DateTime.Parse(cleanDate dateFrom)
                    let dateToStr = DateTime.Parse(cleanDate dateTo).AddDays(1.)
                    let bookCoSrv = ServerModel.getBookCo bookcompany
                    let currentUser = GetCurrentUser ()
                    let! authorized = Alerting.CheckPermission(ServerModel.AppDB, currentUser, ServerModel.Permission.OilReadETS, bookCoSrv) |> Async.AwaitTask
                    if (not authorized) then
                        return { Result = "ERROR"; Message = "Sorry you are not authorized to read BookCo: " + bookcompany; Records = [||];  TotalRecordCount = 0; } else
                    match bookCoSrv with
                    | str when str = "" -> 
                        return { Result = "ERROR"; Message = "Can't find BookCo: " + bookCoSrv; Records = [||];  TotalRecordCount = 0; }
                    | bookCoSrv  ->
                        let! alertPage  = 
                            Async.AwaitTask (Alerting.GetOilAlertsAsync(ServerModel.AppDB, user, code, bookCoSrv, status, key, closed, dateFromStr, dateToStr,jtStartIndex, jtPageSize, jtSorting))
                        return { Result = "OK"; Message = null; 
                                    Records = alertPage.Alerts |> Array.map JsonConvert.SerializeObject 
                                    ; TotalRecordCount = alertPage.Total; }
                with
                | exc -> 
                    System.Diagnostics.Debug.WriteLine (exc.ToString())
                    return { Result = "ERROR"; Message = exc.Message; Records = [||];  TotalRecordCount = 0; }
            }

    [<Rpc>]
    let GetOilAuditAsync (alertCode: string, alertKey: string, bookCo: string, jtStartIndex: int, jtPageSize: int) : Async<JTableResult> = 
        async {
            try
                let bookCoSrv = ServerModel.getBookCo bookCo
                let currentUser = GetCurrentUser ()
                let! authorized = Alerting.CheckPermission(ServerModel.AppDB, currentUser, ServerModel.Permission.OilReadETS, bookCoSrv) |> Async.AwaitTask
                if (not authorized) then
                    return { Result = "ERROR"; Message = "Sorry you are not authorized to read BookCo: " + bookCo; Records = [||];  TotalRecordCount = 0; } else
                let! auditPage = Alerting.GetOilChangesAsync( ServerModel.AppDB, alertCode, alertKey, bookCoSrv, jtStartIndex, jtPageSize) |> Async.AwaitTask
                return { Result = "OK"; Message = null; 
                                    Records = auditPage.Changes |> Array.map JsonConvert.SerializeObject 
                                    ; TotalRecordCount = auditPage.Total; }
            with 
            | exc -> return { Result = "ERROR"; Message = "GetOilAuditAsync: " + exc.Message; Records = [||];  TotalRecordCount = 0; }
        }

    [<Rpc>]
    let DoSomething input =
        let R (s: string) = System.String(Array.rev(s.ToCharArray()))
        async {
            return R input
        }


    [<JavaScript>]
    type SortMethod = | Numeric of string | Alphanumeric of string
    [<JavaScript>]
    type DBTable = | EndurTradeValid | EndurNominationValid | EndurCost | CargoPL
    [<JavaScript>]
    type DBTableResponse = {items: (string * obj) array array; headers: SortMethod array; table: DBTable } 
    [<JavaScript>]
    type TradeResponse = {trades: (string * obj) array array; headers: SortMethod array } 
    [<JavaScript>]
    type NominationResponse = {nominations: (string * obj) array array; headers: SortMethod array } 
    [<JavaScript>]
    type CostResponse = {costs: (string * obj) array array; headers: SortMethod array } 
    [<JavaScript>]
    type BalanceResponse = {balances: (string * obj) array array; headers: SortMethod array } 
    [<JavaScript>]
    type DBResponse<'a> = Response of 'a | Error of DBTable * string
    [<JavaScript>]
    type ServerTradeResponse = 
        { tradeResp: DBResponse<TradeResponse>; 
          nominResp: DBResponse<NominationResponse>; 
          costResp: DBResponse<CostResponse>;
          plResp: DBResponse<BalanceResponse>}
    [<JavaScript>]
    type ResponseReceived<'a> = | Received of 'a | NoInput of string | GeneralError of string
    
    let (|Valid|_|) (str:string) =
        if String.IsNullOrWhiteSpace str then 
            Some(SqlDB.NoSelection) 
        else
            match Int32.TryParse str with
            | false, _ -> None
            | true, num ->
                if (num > 0) then Some(SqlDB.IntSel num) else None
            

    [<Rpc>]
    let GetBookCompany () = async { 
           let session = HttpContext.Current.Session
           return string session.[SessionBookCo] }

    [<Rpc>]
    let SetBookCompany (str:string) = async {
           try
               return [
               let session = HttpContext.Current.Session
               yield "Setting session to " + str
               session.[SessionBookCo] <- str 
               yield "Session: '" +  (string session.[SessionBookCo]) + "'" ] 
            with
            | exc -> 
                return [ "Server Exception:"; exc.Message ; exc.StackTrace ]
            }

    let ComputePL (bookCo:string) (selection:ParsedTrade) (curr: string) : Async<Result<CostBalance, string>> = async {
            match selection with
            | IntSel cargoId ->
                let balances, msg = Alerting.ComputePLWeb( ServerModel.AppDB, bookCo, cargoId, curr)
                return 
                    balances 
                    |> Seq.tryHead 
                    |> Option.fold 
                        (fun _ balance -> Ok balance) 
                        (Result.Error <| "Cargo id not found: " + cargoId.ToString() + " for " + bookCo)
            | NoSelection -> 
                return Result.Error "Select a cargo id to get the PL"
    }
    let PLComputeUSD (bookCo:string) (input:ParsedTrade) : Async<Result<CostBalance, string>> =
        ComputePL bookCo input Alerting.USDCurrency
    let PLComputeEUR (bookCo:string) (input:ParsedTrade) : Async<Result<CostBalance, string>> =
        ComputePL bookCo input Alerting.EURCurrency
    let convertBalance2Table (b: CostBalance) (note: string option) : (string * obj) array =
        let row = [|
            ("PL Currency", b.Currency :> obj);
            ("Pay Amount", b.PayAmount.ToString("N0") :> obj);
            ("Receive Amount", b.ReceiveAmount.ToString("N0") :> obj);
            ("Margin", b.Margin.ToString("N0") :> obj);
            ("Delivery Relevant", (if (b.DeliveryCount > 0) then "Yes" else "No") :> obj);
            ("Receipt Relevant", (if (b.ReceiveCount > 0) then "Yes" else "No") :> obj);
        |] 
        Option.fold (fun r n -> 
            Array.append r [| ("note", n :> obj)|]
        ) row note

    let ComputeCargoPL (input:ParsedTrade) (bookCo:string) : Result<(string * obj) array array, string> =
        let usdBalance = PLComputeUSD bookCo input |> Async.RunSynchronously
        let eurBalance = PLComputeEUR bookCo input |> Async.RunSynchronously
        match usdBalance, eurBalance with
        | Result.Ok usdResult, Result.Ok eurResult ->
            Result.Ok <| [|  convertBalance2Table usdResult None; convertBalance2Table eurResult None |]
        | Result.Ok usdResult, Result.Error eurError ->
            Result.Ok <| [|  convertBalance2Table usdResult (Some eurError);|]
        | Result.Error usdError, Result.Ok eurResult ->
            Result.Ok <| [|  convertBalance2Table eurResult (Some usdError);|]
        | Result.Error usdError,  Result.Error eurError ->
            Result.Error (usdError + if (eurError <> usdError) then ", " + eurError else "")

            
    let extractTrades (db:DB) sel book = Result.Ok <| db.trades sel book
    let extractNominations (db:DB) sel book = Result.Ok <| db.nominations sel book
    let extractCosts (db:DB) sel book = Result.Ok <| db.costs sel book

    let RetrieveItems (bookCo:string) (input:string) (extract) (tableDB: DBTable) (tableName: string) : Async<DBResponse<DBTableResponse>> = async {
        match input with
        | Valid selection -> 
            try
                let tradesTry : Result<(string * obj) array array, string> = extract selection bookCo
                match tradesTry with
                | Result.Ok trades ->
                if (trades |> Array.length > 0) then
                    return Response { 
                    table = tableDB;
                    items = trades;
                    headers = 
                    [|for (k,v) in trades.[0] do 
                        match v with
                        |  :? int
                        |  :? decimal -> yield Numeric k
                        | _ -> yield Alphanumeric k
                        |]
                    }
                else 
                    return Error (tableDB, "no " + tableName + " selected for bookco '" + bookCo + "' and input '" + input+ "'")
                | Result.Error err -> return Error (tableDB, err)
            with e -> return Error (tableDB, e.Message)
        | _ -> return Error (tableDB, "Not a valid " + tableName + " number: " +  input)
        }
    let RetrieveItemsDB (bookCo:string) (input:string) (extract) (tableDB: DBTable) (tableName: string) : Async<DBResponse<DBTableResponse>> =
            let db = DB()
            RetrieveItems bookCo input (extract db) tableDB tableName 
            

    let RetrieveTrades (bookCo:string) (input:string) : Async<DBResponse<DBTableResponse>> = 
        RetrieveItemsDB bookCo input extractTrades EndurTradeValid "trade"

    let RetrieveNominations (bookCo:string) (input:string) : Async<DBResponse<DBTableResponse>> = 
        RetrieveItemsDB bookCo input extractNominations EndurNominationValid "nomination"

    let RetrieveCosts (bookCo:string) (input:string) : Async<DBResponse<DBTableResponse>> = 
        RetrieveItemsDB bookCo input extractCosts EndurCost "cost"

    let RetrievePL (bookCo:string) (input:string) : Async<DBResponse<DBTableResponse>> =
        RetrieveItems bookCo input ComputeCargoPL CargoPL "P/L"
        
    [<Rpc>]
    let RetrieveOilData (bookCo:string) (input:string) : Async<ServerTradeResponse> = async {
        let bookCoSrv = ServerModel.getBookCo bookCo
        let currentUser = GetCurrentUser ()
        let! authorized = Alerting.CheckPermission(ServerModel.AppDB, currentUser, ServerModel.Permission.OilReadETS, bookCoSrv) |> Async.AwaitTask
        if (not authorized) then
            return { tradeResp = Error(EndurTradeValid, "Sorry you are not authorized to read trades of BookCo: " + bookCo ); 
                     nominResp = Error(EndurNominationValid, "Sorry you are not authorized to read nominations of BookCo: " + bookCo ); 
                     costResp =  Error(EndurCost, "Sorry you are not authorized to read costs of BookCo: " + bookCo );
                     plResp =  Error(EndurCost, "Sorry you are not authorized to read PL of BookCo: " + bookCo )} else
        let! oilData =
            [ RetrieveTrades bookCoSrv input; 
              RetrieveNominations bookCoSrv input;
              RetrieveCosts bookCoSrv input; 
              RetrievePL bookCoSrv input]
            |> Async.Parallel
        
        let trades : DBResponse<TradeResponse> =
            oilData
            |> Array.filter (fun d -> 
                match d with
                | Response r -> r.table = EndurTradeValid
                | Error ( t, _) -> t = EndurTradeValid
            )
            |> Array.map(fun d -> 
                match d with
                | Response r -> 
                    Response { trades = r.items; headers = r.headers}
                | Error ( t, e) -> Error ( t, e)
                
            ) |> Array.head

        let nomins : DBResponse<NominationResponse> =
            oilData
            |> Array.filter (fun d -> 
                match d with
                | Response r -> r.table = EndurNominationValid
                | Error ( t, _) -> t = EndurNominationValid
            )
            |> Array.map(fun d -> 
                match d with
                | Response r -> 
                    Response { nominations = r.items; headers = r.headers}
                | Error ( t, e) -> Error ( t, e)
                
            ) |> Array.head

        let costs : DBResponse<CostResponse> =
            oilData
            |> Array.filter (fun d -> 
                match d with
                | Response r -> r.table = EndurCost
                | Error ( t, _) -> t = EndurCost
            )
            |> Array.map(fun d -> 
                match d with
                | Response r -> 
                    Response { costs = r.items; headers = r.headers}
                | Error ( t, e) -> Error ( t, e)
                
            ) |> Array.head

        let balances : DBResponse<BalanceResponse> =
            oilData
            |> Array.filter (fun d -> 
                match d with
                | Response r -> r.table = CargoPL
                | Error ( t, _) -> t = CargoPL
            )
            |> Array.map(fun d -> 
                match d with
                | Response r -> 
                    Response { balances = r.items; headers = r.headers}
                | Error ( t, e) -> Error ( t, e)
                
            ) |> Array.head

        return { tradeResp = trades; nominResp = nomins; costResp =  costs; plResp = balances}
    }

    [<JavaScript>]
    type UpdateResponse = {Result: string; Message: string;}
    [<Rpc>]
    let UpdateOilAlertAsync (record: string) (book: string) (compositeKey: string) = async {
        if record = null 
            then return { Result = "ERROR"; Message = "null record" }; else
        let oilAlert = JsonConvert.DeserializeObject<Alert> (HttpUtility.UrlDecode(record)) 
        if oilAlert = null 
            then return { Result = "ERROR"; Message = "oilAlert record" }; else
        if (String.IsNullOrWhiteSpace(oilAlert.Status) || [|Alerting.CloseFromSys; Alerting.OpenFromSys |] |> Array.contains oilAlert.Status )
            then return { Result = "ERROR"; Message = ("A user cannot update the status to '" + oilAlert.Status + "'");} else
        if (Alerting.AlertStatuses |> Array.contains oilAlert.Status |> not)
            then return { Result = "ERROR"; Message = ("The status " + oilAlert.Status + " is not valid");} else
        let currentUser = GetCurrentUser ()
        let currentUserName = (currentUser.Split('\\') |> Array.last).ToLower()
        let bookingComp = ServerModel.getBookCo book
        let! authorized = Alerting.CheckPermission(ServerModel.AppDB, currentUser, ServerModel.Permission.OilWriteETS, bookingComp) |> Async.AwaitTask
        if ((not authorized) && (oilAlert.AssignedTo = currentUserName |> not) ) 
            then return { Result = "ERROR"; Message = ("User " + currentUser + " is not authorized to write to assigned '" + oilAlert.AssignedTo + "'");} else
        let currCode = oilAlert.AlertCode
        oilAlert.AlertCode <- compositeKey.Split('|').[0]
        let! alertBefore = Alerting.GetLastAlertOf(ServerModel.AppDB, bookingComp, oilAlert) |> Async.AwaitTask
        oilAlert.AlertCode <- currCode
        if alertBefore = null // if we update an alert, such alert must exists
            then return { Result = "ERROR"; Message = "null alertBefore" }; else
        if ((not authorized) && (alertBefore.AssignedTo = currentUserName |> not) ) 
            then return { Result = "ERROR"; Message = ("User " + currentUser + " is not authorized to write from assigned '" + alertBefore.AssignedTo + "'");} else
        if ((not authorized) && (oilAlert.Status = Alerting.Closed) ) 
            then return { Result = "ERROR"; Message = ("User " + currentUser + " is not authorized to to close alerts");} else
        if (authorized && not (String.IsNullOrWhiteSpace alertBefore.AssignedTo)  && not (alertBefore.AssignedTo = currentUserName) && not (oilAlert.AssignedTo = currentUserName) ) 
            then return { Result = "ERROR"; Message = ("Assign the alert to yourself before");} else
        if (Alerting.EscalatedStatuses |> Array.contains alertBefore.Status && not (Alerting.EscalatedStatuses |> Array.contains oilAlert.Status) ) 
            then return { Result = "ERROR"; Message = ("The status " + oilAlert.Status + " is not valid after escalation");} else
        if (Alerting.PostEscalationStatuses |> Array.contains oilAlert.Status && not(Alerting.EscalatedStatuses |> Array.contains alertBefore.Status))
            then return { Result = "ERROR"; Message = ("The status " + oilAlert.Status + " is not valid before escalation");} else
        if (Enum.GetValues(typedefof<Alerting.AlertCodes>) 
            |> Seq.cast<Alerting.AlertCodes> 
            |> Seq.map (fun c -> c.GetEnumDescription()) 
            |> Seq.contains ( oilAlert.AlertCode) |> not)
            then return { Result = "ERROR"; Message = ("The alert code " + oilAlert.AlertCode + " is not valid");} else
        oilAlert.CreationDate <- alertBefore.CreationDate
        oilAlert.LastTransactionDate <- alertBefore.LastTransactionDate
        oilAlert.BookCompany <- alertBefore.BookCompany
        let result_msg = 
            "Updating Oil Alert: " + 
            sprintf "Composite Key: %s - %s %s %s %s %s" compositeKey oilAlert.AlertCode oilAlert.AlertKey oilAlert.BookCompany oilAlert.Commodity oilAlert.Note +  " - " + book
        try 
            let saveOK, error = Alerting.UpdateWithAudit((fun _ -> ()) , ServerModel.AppDB, currentUserName, oilAlert)
            return { Result = (if saveOK then "OK" else "ERROR"); Message = (error + " - " + result_msg);} 
        with 
        | exc ->
            return { Result = "ERROR"; Message = exc.Message };
    }
    
    [<JavaScript>]
    type CreateResponse = {Result: string; Message: string; Record: Alert}
    [<Rpc>]
    let CreateOilAlertAsync (record: string) (book: string) = async {
        let currentUser = GetCurrentUser ()
        let bookingComp = ServerModel.getBookCo book
        let! authorized = Alerting.CheckPermission(ServerModel.AppDB, currentUser, ServerModel.Permission.OilManual, bookingComp) |> Async.AwaitTask
        if (not authorized)
            then return { Result = "ERROR"; Message = "User " + currentUser + " is not authorized to create a manual alert"; Record = null  }; else
        if record = null 
            then return { Result = "ERROR"; Message = "null record"; Record = null }; else
        let oilAlert = JsonConvert.DeserializeObject<Alert> (HttpUtility.UrlDecode(record)) 
        if oilAlert = null 
            then return { Result = "ERROR"; Message = "oilAlert record"; Record = null }; else
        try
            let currentUserName = (currentUser.Split('\\') |> Array.last).ToLower()
            oilAlert.AssignedTo <- currentUserName
            let! addedAlert = Alerting.CreateManualAlert(ServerModel.AppDB, oilAlert) |> Async.AwaitTask 
            return { Result = "OK"; Record = addedAlert; Message = "Record created" }
        with | exc -> 
            let msg = if exc.InnerException <> null then exc.InnerException.Message else exc.Message
            return { Result = "ERROR"; Message = msg; Record = null }
        }

    type ServerLogResponse = LogResponse of string * BookLog[] | LogDetails of string * string * string[] | LogError of string
    [<Rpc>]
    let ReadLogs (bookCo:string) : Async<ServerLogResponse> = 
        async {
            try
                let bookCoSrv = ServerModel.getBookCo bookCo
                let db = new DB()
                let logs = db.logsRead bookCoSrv
                return LogResponse  (bookCo, logs)
            with | exc ->
                return LogError exc.Message
        }

    [<Rpc>]
    let ReadLogDetails (bookCo:string) (asof:string) : Async<ServerLogResponse> =
        async {
            try
                let bookCoSrv = ServerModel.getBookCo bookCo
                let db = new DB()
                let date = DateTime.Parse(cleanDate asof)
                let lines = db.logDetailsRead bookCoSrv date
                return LogDetails (bookCo, asof, lines |> Array.map (fun l -> l.Text))
            with | exc -> 
                return LogError exc.Message
        }

    let internal internalReadAnalysts bookCo =
        let bookCoSrv = ServerModel.getBookCo bookCo
        let db = new DB()
        db.analysts bookCoSrv 
    
    type ServerAnalystResponse = AnalystResponse of string * Analyst[] | AnalystError of string
    [<Rpc>]
    let ReadAnalysts bookCo =
        async {
            try
                let analysts = internalReadAnalysts bookCo
                return AnalystResponse (bookCo, analysts)
            with |exc ->
                return AnalystError exc.Message
        }
    
    [<Rpc>]
    let UpdateAnalyst bookCo analyst =
        async {
            try
                let bookCoSrv = ServerModel.getBookCo bookCo
                let currentUser = GetCurrentUser ()
                let! authorized = Alerting.CheckPermission(ServerModel.AppDB, currentUser, ServerModel.Permission.OilWriteETS, bookCoSrv) |> Async.AwaitTask
                if (not authorized) then return () else
                let db = new DB()
                db.analystUpdate bookCoSrv analyst
            with | _ -> ()
        }
    [<Rpc>]
    let DeleteAnalyst bookCo analyst =
        async {
            try
                let bookCoSrv = ServerModel.getBookCo bookCo
                let currentUser = GetCurrentUser ()
                let! authorized = Alerting.CheckPermission(ServerModel.AppDB, currentUser, ServerModel.Permission.OilWriteETS, bookCoSrv) |> Async.AwaitTask
                if (not authorized) then return () else
                let db = new DB()
                db.analystDelete bookCoSrv analyst
            with | _ -> ()
        }
    [<Rpc>]
    let InsertAnalyst bookCo analyst =
        async {
            try
                let bookCoSrv = ServerModel.getBookCo bookCo
                let currentUser = GetCurrentUser ()
                let! authorized = Alerting.CheckPermission(ServerModel.AppDB, currentUser, ServerModel.Permission.OilWriteETS, bookCoSrv) |> Async.AwaitTask
                if (not authorized) then return () else
                let db = new DB()
                db.analystCreate bookCoSrv analyst
            with | _ -> ()
        }
      
        
    type ServerImportResponse = ImportOkResponse of string * string | ImportError of string
    [<Rpc>]
    let SingleImport (bookCo:string) (asofStr:string) : Async<ServerImportResponse> =
        async {
            try
                let bookCoSrv = ServerModel.getBookCo bookCo
                let asofDate = DateTime.Parse(cleanDate asofStr)
                let currentUser = GetCurrentUser ()
                let! authorized = Alerting.CheckPermission(ServerModel.AppDB, currentUser, ServerModel.Permission.OilWriteETS, bookCoSrv) |> Async.AwaitTask
                if authorized then
                    Program.RunImportAsof(asofDate, bookCoSrv);
                    return ImportOkResponse (bookCo, asofStr)
                else  
                    return ImportError (currentUser + " is not authorized to run asof import")
            with |exc ->
                return ImportError exc.Message
        }
    [<Rpc>]
    let MultiImport (bookCo:string) (fromDateStr:string) (toDateStr:string) : Async<ServerImportResponse> =
        async {
            try
                let bookCoSrv = ServerModel.getBookCo bookCo
                let asofFromDate = DateTime.Parse(cleanDate fromDateStr)
                let asofToDate = DateTime.Parse(cleanDate toDateStr)
                let currentUser = GetCurrentUser ()
                let! authorized = Alerting.CheckPermission(ServerModel.AppDB, currentUser, ServerModel.Permission.OilWriteETS, bookCoSrv) |> Async.AwaitTask
                if authorized then
                    let loop_msg = ref ""
                    let ok  = Program.LoopRunImportAsof(asofFromDate, asofToDate, bookCoSrv, loop_msg);
                    if ok then
                        return ImportOkResponse (bookCo, fromDateStr + " - " + toDateStr)
                    else 
                        return ImportError ("Multi import error: " + !loop_msg)
                else  
                    return ImportError (currentUser + " is not authorized to run asof import")
            with |exc ->
                return ImportError exc.Message
        }

    type ReportDownloadSuccess = {Message: string; }
    type ReportDownloadError = {Error: string}
    type ReportDownload = Ok of ReportDownloadSuccess | Ko of ReportDownloadError
    type ReportResult = {ReportName: string; Result: ReportDownload}    
    let showEmpty str =
        if System.String.IsNullOrWhiteSpace str then "-" else str

    let RunInternalReport (bookCo:string) (rptParams: Map<string,string>) (runDBLib: Map<string,string> -> string -> Async<'a[]>) : Async<ReportResult> =
        async {
            try
                let bookCoSrv = ServerModel.getBookCo bookCo 
                let currentUser = GetCurrentUser ()
                let! authorized = Alerting.CheckPermission(ServerModel.AppDB, currentUser, ServerModel.Permission.OilReadETS, bookCoSrv) |> Async.AwaitTask
                if (not authorized) then
                    return {ReportName = Import2TSS.Report.ExtractReport; Result = Ko {Error = "Sorry you are not authorized to read BookCo: " + bookCo}} else
                let currentUserName = (currentUser.Split('\\') |> Array.last).ToLower()
                let userPath = String.Format("Excel/OilReport_{0}.xlsx", currentUserName)
                let userPathSrv= HttpContext.Current.Server.MapPath( userPath)
                let! flatExtra = runDBLib rptParams bookCoSrv
                let (isOk, log) =  Import2TSS.Report.WriteCollectionToExcelTable(userPathSrv, Import2TSS.Report.ExtractReport, flatExtra)
                let selParamsText =
                    rptParams
                    |> Map.toArray
                    |> Array.map (fun (k,v) -> k + ": " + showEmpty v)
                return {ReportName = Import2TSS.Report.ExtractReport; Result = Ok {Message = 
                    Import2TSS.Report.ExtractReport + " executed for " + String.Join(",",selParamsText) 
                    + " -> " + if isOk then "ok" else log}}
            with |exc ->
                return {ReportName = Import2TSS.Report.ExtractReport; Result = Ko {Error = exc.Message}}
        }

    let runAlertExtractDBLib (rptParams: Map<string,string>) (bookCoSrv: string) : Async<FlatExtractAlert[]> = async {
             let! extracted = Import2TSS.Report.GetExtractReport(ServerModel.AppDB, Dictionary(rptParams), bookCoSrv)  |> Async.AwaitTask
             return
                 extracted
                 |> Array.map(fun r -> 
                     FlatExtractAlert.FromExtract(r.alert,r.lastUpdate, 
                         r.nomin_receipt, r.nomin_delivery, r.trade))
         }

    let runEscalationReportDBLib (rptParams: Map<string,string>) (bookCoSrv: string) : Async<AuditAlert[]> = async {
             let! extracted = Import2TSS.Report.GetEscalatedAlerts(ServerModel.AppDB, Dictionary(rptParams), bookCoSrv)  |> Async.AwaitTask
             return extracted
         }


    [<Rpc>]
    let ExtractReport (bookCo:string) (rptParams: Map<string,string>) : Async<ReportResult> =
        RunInternalReport bookCo rptParams runAlertExtractDBLib

    [<Rpc>]
    let EscalationReport (bookCo:string) (rptParams: Map<string,string>) : Async<ReportResult> =
        RunInternalReport bookCo rptParams runEscalationReportDBLib
    
//[<JavaScript>]
//module Report =
//    [<Literal>]
//    let AlertExtract = "Alert Extract"
//    [<Literal>]
//    let Escalation = "Report Escalation"
//    [<Literal>]
//    let CptyFreq = "Counterparty Frequency"
//    [<Literal>]
//    let ZeroAlloc = "Zeroed Allocations"
