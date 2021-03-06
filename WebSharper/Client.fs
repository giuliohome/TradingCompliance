﻿namespace ClientServerTable

open WebSharper
open WebSharper.JavaScript
open WebSharper.UI
open WebSharper.UI.Client
open WebSharper.UI.Html
open Server
open WebSharper.JQuery
open SqlLib
open ClientBase
open ARecs
open WebSharper.Moment


[<JavaScript>]
module Client = 

    type DataInput = {input: string; book: string; entity_type: string}
    let rvInput = Var.Create {input = ""; book = ""; entity_type = ""}
    let submit = Submitter.CreateOption rvInput.View
    let htmlInput = Var.Create ""
    let htmlSubmit = Submitter.CreateOption htmlInput.View
    let ignoreView = 
        htmlSubmit.View.MapAsync(function
            | None -> async { () }
            | Some html_input -> 
                async {
                    rvInput.Update(fun curr -> {curr with input = html_input})
                    submit.Trigger()
                }
        )

    

    let refreshAlertView () =
        refreshAlerts.Trigger()
        auditInput.Set NoAlertSelection
        auditSubmit.Trigger()

    let vAnalystsResp =
        refreshAnalysts.View.MapAsync(function
            | book -> 
                Console.Log("vAnalystsResp book: ", book)
                async { 
                    return! Server.ReadAnalysts book })

    type Helper =
        {
            selected: bool
            value: string
        }

    let DropDown_OnChange (el: Dom.Element) (ev: Dom.Event) (action: Helper -> unit) = 
        let opts = el?options : Helper[]
        opts
        |> Array.filter(fun x -> x.selected)
        |> Array.iter action 
    

    let RptResultVar  = Var.Create (None: ReportResult option)
    let SelRptVar = Var.Create ""
    let RptRunningVar = Var.Create false
    let CptyFreqMinReceiptVar = Var.Create "5"
    let CptyFreqMinDeliveryVar = Var.Create "5"
    let TradeCreateAfterVar = Var.Create cptyFreqTradeAfter
    let CptyFreqTradeAfter_Changed value = 
        Var.Set TradeCreateAfterVar value
    let TradeCreateBeforeVar = Var.Create cptyFreqTradeBefore
    let CptyFreqTradeBefore_Changed value = 
        Var.Set TradeCreateBeforeVar value
    let AlertExtractAnalystVar = Var.Create ""
    let AlertExtractCptyVar = Var.Create ""
    let AlertExtractAnalyst_OnChange (el: Dom.Element) (ev: Dom.Event) = 
        DropDown_OnChange el ev (fun x -> 
               Var.Set AlertExtractAnalystVar x.value
            )

    let InitAnalistView el = 
        refreshAnalistView bookVar.Value 

    let RunCptyFreqReport () =
        async {
            Var.Set RptRunningVar true
            Var.Set RptResultVar None
            let minReceipt = CptyFreqMinReceiptVar.Value
            let minDelivery = CptyFreqMinDeliveryVar.Value
            let tradesAfter = TradeCreateAfterVar.Value
            let tradesBefore = TradeCreateBeforeVar.Value
            let rptParams = [ (Import2TSS.RptParameter.MinFreqReceipt, minReceipt); 
                              (Import2TSS.RptParameter.MinFreqDelivery, minDelivery); 
                              (Import2TSS.RptParameter.TradesCreatedAfter, tradesAfter); 
                              (Import2TSS.RptParameter.TradesCreatedBefore, tradesBefore)] |> Map.ofSeq
            let! result = Server.CptyFreqRpt bookVar.Value  rptParams
            Var.Set RptResultVar <| Some result
            Var.Set RptRunningVar false
        } |> Async.StartImmediate

    let RunEscalationReport () =
        async {
            Var.Set RptRunningVar true
            Var.Set RptResultVar None
            let analyst = AlertExtractAnalystVar.Value
            let rptParams = [ (Import2TSS.RptParameter.Analyst, analyst); ] |> Map.ofSeq
            let! result = Server.EscalationReport bookVar.Value  rptParams
            Var.Set RptResultVar <| Some result
            Var.Set RptRunningVar false
        } |> Async.StartImmediate
    
    let RunAlertExtract () = 
        async {
            Var.Set RptRunningVar true
            Var.Set RptResultVar None
            let analyst = AlertExtractAnalystVar.Value
            let cpty = AlertExtractCptyVar.Value
            let rptParams = [ (Import2TSS.RptParameter.Analyst, analyst); (Import2TSS.RptParameter.Counterparty, cpty) ] |> Map.ofSeq
            let! result = Server.ExtractReport bookVar.Value  rptParams
            Var.Set RptResultVar <| Some result
            Var.Set RptRunningVar false
        } |> Async.StartImmediate
    
    let DownloadReport () = 
        ()

    let Report_OnChange (el: Dom.Element) (ev: Dom.Event) = 
        DropDown_OnChange el ev (fun x -> 
               Var.Set RptResultVar None
               Var.Set SelRptVar x.value
            )
    
    let Analyst_OnChange (el: Dom.Element) (ev: Dom.Event) = 
        DropDown_OnChange el ev (fun x -> 
           alertSelVar.Update (fun curr -> {curr with analyst = x.value})
           refreshAlertView() 
        )
    
    let Status_OnChange (el: Dom.Element) (ev: Dom.Event) = 
        DropDown_OnChange el ev (fun x ->  
           alertSelVar.Update (fun curr -> {curr with status = x.value})
           refreshAlertView() 
        )

    let AlertCode_OnChange (ev: Dom.Event) = 
        Console.Log("AlertOnChange fired") 
        let opts = ev.Target?options : Helper []
        let selcodes =
            opts 
            |> Array.filter(fun x -> x.selected)
            |> Array.map(fun x -> "'" + x.value + "'")
        alertSelVar.Update (fun curr -> {curr with codes = selcodes}) 
        refreshAlertView() 
   
    let reactiveAnalyst book okFun = 
        async {
            let! analystResp = Server.ReadAnalysts book
            match  analystResp with
            | AnalystError _ -> 
                okFun [||]
            | AnalystResponse (_, analysts)  ->
                okFun analysts
        } |> Async.StartImmediate

    let populateTableRender (alertSel: AlertSelection)  codes statuses = 
        populateTable alertSel codes statuses
        |> reactiveAnalyst alertSel.book

    let populateAuditTableRender (detail: AuditData) =
        populateAuditTable detail
        |> reactiveAnalyst detail.bookCo

    let Alerts (alertSel: AlertSelection) (codes: string array) (statuses: string array) = 
        Console.Log("receiving the responsive BookCo: " + alertSel.book)
        div [on.afterRender(fun el -> populateTableRender alertSel codes statuses) ] [
            div [attr.id "AlertDiv"; attr.style "clear: both;"] [
                h1 [attr.id "AlertHeader"] [text "All Alerts"]
                div [attr.id "AlertTable"] []
            ]
            Doc.BindView (function
            | None -> Doc.Empty
            | Some selection ->
                match selection with
                | NoAlertSelection -> Doc.Empty
                | AlertSelection detail ->
                    div [attr.id "DetailDiv";  ] [ 
                        h1 [attr.id "DetailHeader"; attr.style "clear: both;"] []
                        h1 [attr.id "AuditHeader"; attr.style "clear: both;"] [text <| "Audit for " + detail.alertCode +  " alert " + detail.alertKey + " of " + detail.bookCo]
                        div [attr.id "AuditTable"; attr.style "clear: both;"; on.afterRender (fun _ -> populateAuditTableRender detail)] []
                    ]) auditSubmit.View
        ]
    




    let SetInitBookCo (bookCo:string) = 
        async {
            Console.Log("Setting initial BookCo to " + bookCo)
            let! initial = Server.GetBookCompany()
            Console.Log("The initial booking company is already: " + initial)
            let sel = JS.Document.GetElementById(DropDownList) |> As<HTMLSelectElement>
            let bookCoFinal = if System.String.IsNullOrWhiteSpace(initial) then bookCo else initial
            if  ( sel <> null && [EtsSpA; EtsInc] |> List.contains bookCoFinal) then
                let opt = sel.NamedItem(bookCoFinal) // assuming attr.id is correctly set
                opt.Selected <- true
                Console.Log("Calling Server.SetBookCompany")
                if System.String.IsNullOrWhiteSpace(initial) then 
                    let! check_list = Server.SetBookCompany bookCoFinal
                    check_list |> List.iter Console.Log
            alertSelVar.Update (fun curr -> {curr with book = bookCoFinal}) 
            refreshAlerts.Trigger()
            rvInput.Update (fun curr -> {curr with book = bookCoFinal})
            submit.Trigger()
            refreshLogViewFromBook bookCoFinal
            refreshAnalistView bookCoFinal
        } |> Async.StartImmediate

    let cleanSelectOtpions (elems: string array) =
        elems
        |> Array.iter(fun elem ->
            let initialSel = JQuery.Of(elem + " option:selected")
            Console.Log("initialSel: ", initialSel)
            JS.Inline("$0.prop($1,$2)", initialSel, "selected", false)
        )

    let DateFrom_Changed value =
        alertSelVar.Update (fun curr -> {curr with dateFrom = value}) 
        refreshAlertView()
    let DateTo_Changed value =
        alertSelVar.Update (fun curr -> {curr with dateTo = value}) 
        refreshAlertView()
    
    let ResponsiveAlerts (bookCo:string) (codes: string array) (statuses: string array) = 
        SetInitBookCo bookCo

        

        div [
                on.afterRender (fun el -> 
                    cleanSelectOtpions [| "#AlertCodeSelect"; "#StatusSelect" |]
                    datePicker "#DateFrom" dateStdFormat DateFrom_Changed initDateFrom
                    datePicker "#DateTo" dateStdFormat DateTo_Changed initDateTo
                    let triggerMe = JQuery.Of("#AlertCodeSelect")
                    JS.Inline("$0.on('change', $1);", triggerMe, AlertCode_OnChange)
                    // see https://forums.websharper.com/topic/87182
                    let chosen_select = JQuery.Of(".chosen-select")
                    JS.Inline("$0.chosen()", chosen_select)
                    ); 
                attr.id "AlertHolder"; attr.style "padding-bottom: 5px;"] [
            div [attr.id "OptionsDiv"; ] [
                h2 [] [text "Alert Options"]
                div [attr.id "SelectionDiv"; attr.style "padding: 10px;"] [
                    div [attr.style "padding-bottom: 5px;"] [
                        div [attr.style "width:150px; display: inline-block;"] [text "Alert code"]
                        select [attr.id "AlertCodeSelect"; 
                                 attr.``class`` "chosen-select";attr.multiple ""; attr.style "width:150px"] [ 
                            codes 
                            |> Array.map( fun code ->
                                Elt.option [] [text code] :> Doc ) 
                            |> Doc.Concat
                            ]
                        label [attr.style "margin-left: 60px; width: 100px; font-weight: normal;"] [text "Alert Key"]
                        Doc.Input [attr.style "width:150px;"; on.change (fun _ _ -> refreshAlertView ());] (Lens alertSelVar.V.alertKey)
                    ]
                    div [attr.style "padding-bottom: 5px;"] [
                        DropDownInput "Assigned To" "150px" "UserSelect" "150px" vAnalystsResp Analyst_OnChange alertSelVar.Value.analyst
                        
                        label [attr.style "margin-left: 60px; width: 100px; font-weight: normal;"] [text "Status"]
                        select [attr.id "StatusSelect"; on.change Status_OnChange;
                            attr.``class`` "btn btn-primary dropdown-toggle"; attr.style "width:150px"] [
                            Elt.option [] []; 
                            statuses
                            |> Array.map (fun status -> Elt.option [] [text status] :> Doc)
                            |> Doc.Concat
                        ]    
                    ]
                    div [] [
                        span [attr.style "width:153px; height: 50px; float: left;"] [text "Date Range"]
                        span [attr.style "width:50px; float: left; "] [text "From"]
                        input [attr.id "DateFrom"; attr.size "10"; attr.``type`` "text"] []
                        label [attr.style "margin-left: 60px; width: 50px; display: inline-block; font-weight: normal"] [text "To"]
                        input [attr.id "DateTo"; attr.size "10"; attr.``type`` "text"] []
                    ]
                    div [attr.style "clear: both; padding-bottom: 5px;"] [
                        span [attr.style "width:150px; display: inline-block;"] [text "Display Closed Alerts"]
                        input [on.change (fun el _ -> 
                                    Var.Set alertSelVar { alertSelVar.Value with showClosed =  (el?value = "true") }
                                    refreshAlertView ()
                                ); 
                                      attr.style "width: 30px;"; attr.``type`` "radio"; 
                                      attr.name "Closed"; attr.value "false"; attr.``checked`` ""] []       
                        text "No"
                        input [on.change (fun el _ -> 
                                    Var.Set alertSelVar { alertSelVar.Value with showClosed =  (el?value = "true") }
                                    refreshAlertView ()
                                );  
                               attr.style "width: 30px;"; attr.``type`` "radio"; 
                               attr.name "Closed"; attr.value "true"] []
                        text "Yes"
                    ]
                    div [] [
                    ]
                ]
            ]
            refreshAlerts.View
            |> View.WithInitOption
            |> Doc.BindView (function
                | None -> 
                    Doc.Concat [
                    img [attr.src @"Content/transparent-blue-loading-image-gif-5.gif"; attr.style "width:250px;height:250px;"] []
                    text "Please wait, db retrieval is running..." ]
                | Some response when response.book = "" -> Doc.Empty
                | Some response -> Alerts response codes statuses
                )
        ]


        
       

    let Main (table_url:  string) =
        Console.Log ("table_url from server: '" + table_url + "'")
        let rvInput = Var.Create ""
        let submit = Submitter.CreateOption rvInput.View
        let vReversed =
            submit.View.MapAsync(function
                | None -> async { return "" } 
                | Some input -> Server.DoSomething input
            )
        div [] [
            Doc.Input [] rvInput
            Doc.Button "Send" [] submit.Trigger
            hr [] []
            h4 [attr.``class`` "text-muted"] [text "The server responded:"]
            div [attr.``class`` "jumbotron"] [h1 [] [textView vReversed]]
        ]

    let Inspector (table_name: string) =
        a [ attr.``class`` "button ml-1"
            on.click (fun el ev -> 
                let jq_table = JQuery.Of("#" + table_name)
                JS.Inline ("$0.data('table').toggleInspector()", jq_table) |> ignore )
            ]  [span [attr.``class`` "mif-cog"] []]
    
    let tableWithInspector table_name html = Doc.Concat [
            div [] [Inspector table_name];
            table [ attr.``class`` "table striped table-border mt-4"; 
                    attr.``data-`` "role" "table"; 
                    attr.``data-`` "horizontal-scroll" "true"; 
                    attr.id table_name] 
                  html
            ]
    
    let convertObj (x:obj) =
        match x with
        | :? ServerModel.HoverCell as c -> 
            div [attr.``class`` "containerT"] [
                p [attr.``class`` "special_parT cell_parT"] [text c.Base]
                div [attr.``class`` "overlayT cell_parT"] [
                    div [attr.``class`` "textT"] [ text c.Hover ]] ]
        | x -> text <| string x


    let buildTableHtml 
        (resp: DBResponse<'a>) 
        (itemsFromResp: 'a -> (string * obj) array array)  
        (table_name: string) = 
                match resp with
                | Response tableResp ->
                    let itemsDB = itemsFromResp tableResp
                    let headers = tableResp?headers
                    Console.Log(table_name + " response: " + string itemsDB.Length)
                    Doc.Concat[
                        (if Array.isEmpty itemsDB then
                            p [] [text <| "No " + table_name + " selected"]
                        else
                            tableWithInspector (table_name + "-table") [
                                yield thead [] [
                                    tr [] [
                                        for v in headers do
                                            match v with
                                            | Numeric k -> yield th [attr.``class`` "sortable-column"; attr.``data-`` "format" "number"] [text k]
                                            | Alphanumeric k ->  yield th [attr.``class`` "sortable-column"] [text k]
                                    ] ]
                                yield tbody [] [
                                    for tup in itemsDB do
                                        yield tr [] [
                                            for (k,v) in tup do
                                                yield td [] [convertObj v]
                                        ] ]
                            ]
                         )
                    ]  
                | Error (table ,err) -> 
                    Console.Log("from table " + table.ToString() + "response error: " + err)
                    div [] [
                    text err
                    ]

    let TableRetrieve (input_received: ResponseReceived<ServerTradeResponse>) = 
        match input_received with
        | Received input ->
            let tradeTableDoc = buildTableHtml input.tradeResp (fun resp -> resp.trades) "trade"
            let nominTableDoc = buildTableHtml input.nominResp (fun resp -> resp.nominations) "nomin"
            let costTableDoc = buildTableHtml input.costResp (fun resp -> resp.costs) "cost"
            let plTableDoc = buildTableHtml input.plResp (fun resp -> resp.balances) "pl"
            div [] [
                h2 [] [text "Trades"]
                tradeTableDoc; 
                h2 [] [text "Nominations"]
                nominTableDoc; 
                h2 [] [text "Costs"]
                costTableDoc;
                h2 [] [text "P&L"]
                plTableDoc;
            ]
        | NoInput str ->
            Console.Log("response no input: " + str)
            div [] [ 
            text str
            ]
        | GeneralError err ->
            Console.Log("response error: " + err)
            div [] [
            h2 [] [text "¯\_(ツ)_/¯"]   
            text err
            ]
            

    let fsz = attr.style "font-size:14px"
    let analystCols = tr [] [
                    th [fsz] [text "ID"]
                    th [fsz] [text "Name"]
                    th [fsz] [text "Surname"]
                    th [fsz] [text "Update"]
                    th [fsz] [text "Delete"]
                ]
    let logCols = tr [] [
                    th [fsz] [text "Select"]
                    th [fsz] [text "As of"]
                    th [fsz] [text "Result"]
                    th [fsz] [text "Start"]
                    th [fsz] [text "End"]
                ]

    let tableLogs book logs =
        div [] [
            table [ attr.``class`` "table table-striped table-bordered"; 
                    attr.cellspacing "0"; on.afterRender (fun el -> 
                        JQuery.Of(el)?DataTable() 
                    )][
                thead [] [
                    logCols
                ]
                tbody [] [
                    for (log: SqlDB.BookLog) in logs do
                        let asof_date = Moment(log.AsofDate).ToUtc().Add(1,"d")
                        yield tr [] [
                            td [fsz] [
                                Doc.Button "Details" [] (fun () -> 
                                    refreshLogViewFromSelection <| Some (System.DateTime(asof_date.Year(), asof_date.Month() + 1, asof_date.Date()))
                                )
                            ]
                            td [fsz] [text (asof_date.Format("DD/MM/YYYY"))]
                            td [fsz] [text log.Status]
                            td [fsz] [text (log.StartDate.ToShortDateString() + " " + log.StartDate.ToShortTimeString())]
                            td [fsz] [text (log.EndDate.ToShortDateString() + " " + log.EndDate.ToShortTimeString())]
                        ]
                ]
            ]
        ]
    
    let logDetails (book:string) (date:string) (lines: string[]) =
        div [] [
            p [] [Doc.Button "Back to log lists" [] (fun () -> refreshLogViewFromSelection None)]
            h2 [] [text <| "Log Rows " + date]
            table [] [
                 thead [] [
                    tr [] [
                        th [fsz] [text "Row"]
                    ]
                 ]
                 tbody [] [
                    for (line: string) in lines do
                        yield tr [] [
                            td [fsz] [text line]
                        ]
                 ]
            ]
        ]

    let tableAnalysts (book:string) analysts =
        Console.Log("tableAnalysts book: ", book)
        let NewIdVar = Var.Create ""
        let NewNameVar = Var.Create ""
        let NewSurnameVar = Var.Create ""

        div [attr.style "width:600px"] [

            table [ attr.``class`` "table table-striped table-bordered"; 
                    attr.cellspacing "0"; on.afterRender (fun el -> 
                        JQuery.Of(el)?DataTable() 
                    )][
                thead [] [
                    analystCols
                ]
                tbody [] [
                    for (analyst: SqlDB.Analyst) in analysts do  
                        let NameVar = Var.Create analyst.Name
                        let SurnameVar = Var.Create analyst.Surname
                        yield tr [] [
                        td [fsz] [text analyst.UserName]
                        td [fsz] [Doc.Input [] NameVar]
                        td [fsz] [Doc.Input [] SurnameVar]
                        td [fsz] [
                            Doc.Button "Update" [] (fun () -> 
                                callUpdateAnalyst book {analyst with Name = NameVar.Value; Surname = SurnameVar.Value})
                        ]
                        td [fsz] [
                            Doc.Button "Delete" [] (fun () -> 
                                callDeleteAnalyst book {analyst with Name = NameVar.Value; Surname = SurnameVar.Value}) 
                        ]
                    ] 
                ]
            ]

           
            p [] [text "Enter a new analyst"]
            form [attr.style "width:100px"] [
                div [attr.``class`` "form-group"][
                    label [] [text "ID"]
                    Doc.Input [attr.``class`` "form-control"; ] NewIdVar
                ]
                div [attr.``class`` "form-group"][
                    label [] [text "Name"]
                    Doc.Input [attr.``class`` "form-control"; ] NewNameVar
                ]
                div [attr.``class`` "form-group"][
                    label [] [text "Surname"]
                    Doc.Input [attr.``class`` "form-control"; ] NewSurnameVar
                ]
                Doc.Button "Create" [attr.``class`` "btn btn-primary"] 
                    (fun () -> callInsertAnalyst book {UserName = NewIdVar.Value; Name = NewNameVar.Value; Surname = NewSurnameVar.Value})
            ]

        ]    

    let refreshPivot sel =    
        async {
            let data = sel2jtable sel
            let! alertPage = Server.GetOilAlertsAsync (data.bookcompany, data.user, data.code, data.status, data.key, data.closed, data.dateFrom, data.dateTo, 0, 10000, "AlertKey ASC,CreationDate ASC,AlertCode ASC") 
            updateAssignedBadges alertPage.AssignedAlerts
            let alerts = alertPage.Records |> Array.map Json.Deserialize
            JSPivot alerts
        } |> Async.StartImmediate

    let buildPivot (sel:AlertSelection) = 
        div [on.afterRender (fun el -> refreshPivot sel)] [
            div [attr.id "AlertPivot"] []
        ]
        
    let RptRunView runAlertExtract =
        RptRunningVar.View
        |> Doc.BindView (function
            | true -> 
                Doc.Concat [
                img [attr.src @"Content/transparent-blue-loading-image-gif-5.gif"; attr.style "width:250px;height:250px;"] []
                text "Please wait, db retrieval is running..." ]
            | false ->
                Doc.Button "Run Report" [attr.``class`` "btn btn-primary"] runAlertExtract
        )
    let RptResultView downloadLink =
        RptResultVar.View
        |> Doc.BindView (function
            | None -> Doc.Empty
            | Some rptRes ->  
                match rptRes.Result with
                | Ko err -> text err.Error
                | Ok succ -> 
                    div [] [
                        text succ.Message
                        br [] []
                        downloadLink
                    ]
        )
    let RetrievePivot (initial_book:string) = 
        

        let downloadLink = a [attr.href (getUrl EndPoint.Download)] [text "Download"]
        SetInitBookCo initial_book

        div [on.afterRender (fun _ -> 
                    datePicker "#DateFrom" dateStdFormat DateFrom_Changed initDateFrom
                    datePicker "#DateTo" dateStdFormat DateTo_Changed initDateTo)] [
          h2 [] [text "Alert Options"]
          div [] [
                span [attr.style "width:153px; height: 50px; float: left;"] [text "Date Range"]
                span [attr.style "width:50px; float: left; "] [text "From"]
                input [attr.id "DateFrom"; attr.size "10"; attr.``type`` "text"] []
                label [attr.style "margin-left: 60px; width: 50px; display: inline-block; font-weight: normal"] [text "To"]
                input [attr.id "DateTo"; attr.size "10"; attr.``type`` "text"] []
          ]
          div [attr.style "clear: both; padding-bottom: 5px;"] [
                span [attr.style "width:150px; display: inline-block;"] [text "Display Closed Alerts"]
                input [on.change (fun el _ -> 
                            Var.Set alertSelVar { alertSelVar.Value with showClosed =  (el?value = "true") }
                            refreshAlertView ()
                        ); 
                                attr.style "width: 30px;"; attr.``type`` "radio"; 
                                attr.name "Closed"; attr.value "false"; attr.``checked`` ""] []       
                text "No"
                input [on.change (fun el _ -> 
                            Var.Set alertSelVar { alertSelVar.Value with showClosed =  (el?value = "true") }
                            refreshAlertView ()
                        );  
                        attr.style "width: 30px;"; attr.``type`` "radio"; 
                        attr.name "Closed"; attr.value "true"] []
                text "Yes"
            ]
          h2 [] [text "Oil Pivot"]
          refreshAlerts.View
            |> View.WithInitOption
            |> Doc.BindView (function
                | None -> 
                    Doc.Concat [
                    img [attr.src @"Content/transparent-blue-loading-image-gif-5.gif"; attr.style "width:250px;height:250px;"] []
                    text "Please wait, db retrieval is running..." ]
                | Some response when response.book = "" -> text "no book company selected"
                | Some response -> buildPivot response
            )
          br [][]
          h2 [] [text "Reports"]
          div [attr.style "padding-bottom: 5px;"] [
              span [attr.style "width: 120px; display: inline-block;"] [text "Report Name"]
              select [ on.change Report_OnChange;
                    attr.``class`` "btn btn-primary dropdown-toggle";] [
                Elt.option [attr.selected "";] []
                Elt.option [] [text Import2TSS.Report.EscalationReport]
                Elt.option [] [text Import2TSS.Report.ExtractReport]
                Elt.option [] [text Import2TSS.Report.CptyFreq]
                Elt.option [] [text Import2TSS.Report.ZeroAlloc]
              ]
          ]
          Doc.BindView (function
            | "" -> Doc.Empty
            | Import2TSS.Report.CptyFreq ->
                div [on.afterRender InitAnalistView] [
                    div [attr.style "padding-bottom: 5px;"] [
                        span [attr.style "width: 120px; display: inline-block;"] [text Import2TSS.RptParameter.MinFreqReceipt]
                        Doc.Input [attr.style "width: 200px;"] CptyFreqMinReceiptVar
                    ]
                    div [attr.style "padding-bottom: 5px;"] [
                        span [attr.style "width: 120px; display: inline-block;"] [text Import2TSS.RptParameter.MinFreqDelivery]
                        Doc.Input [attr.style "width: 200px;"] CptyFreqMinDeliveryVar
                    ]
                    br [] []
                    div [attr.style "padding-bottom: 5px;"; on.afterRender(fun _ -> 
                            datePicker "#CptyFreqTradeAfter" dateStdFormat CptyFreqTradeAfter_Changed cptyFreqTradeAfter)] [
                        span [attr.style "width: 120px; display: inline-block;"] [text Import2TSS.RptParameter.TradesCreatedAfter]
                        input [attr.id "CptyFreqTradeAfter"; attr.style "width: 200px;"] []
                    ]
                    div [attr.style "padding-bottom: 5px;"; on.afterRender(fun _ -> 
                            datePicker "#CptyFreqTradeBefore" dateStdFormat CptyFreqTradeBefore_Changed cptyFreqTradeBefore)] [
                        span [attr.style "width: 120px; display: inline-block;"] [text Import2TSS.RptParameter.TradesCreatedBefore]
                        input [attr.id "CptyFreqTradeBefore"; attr.style "width: 200px;"] []
                    ]
                    br [] []
                    RptRunView RunCptyFreqReport
                    RptResultView downloadLink
                ]
            | Import2TSS.Report.EscalationReport ->
                div [on.afterRender InitAnalistView] [
                    div [attr.style "clear: both; padding-bottom: 5px;"] [ 
                        DropDownInput Import2TSS.RptParameter.Analyst "120px" "UserSelect" "200px" vAnalystsResp AlertExtractAnalyst_OnChange AlertExtractAnalystVar.Value
                    ]
                    br [] []
                    RptRunView RunEscalationReport
                    RptResultView downloadLink
                ]
            | Import2TSS.Report.ExtractReport -> 
                div [on.afterRender InitAnalistView] [
                    div [attr.style "padding-bottom: 5px;"] [
                        span [attr.style "width: 120px; display: inline-block;"] [text Import2TSS.RptParameter.Counterparty]
                        Doc.Input [attr.style "width: 200px;"] AlertExtractCptyVar
                    ]
                    div [attr.style "clear: both; padding-bottom: 5px;"] [ 
                        DropDownInput Import2TSS.RptParameter.Analyst "120px" "UserSelect" "200px" vAnalystsResp AlertExtractAnalyst_OnChange AlertExtractAnalystVar.Value
                    ]
                    br [] []
                    RptRunView RunAlertExtract
                    RptResultView downloadLink
                ]
            | rpt -> 
                text <| "TODO: " + rpt
            ) SelRptVar.View
        ]

    let RetrieveAnalysts (initial_book:string) = 
        Console.Log("RetrieveAnalysts book: ", initial_book)
        SetInitBookCo initial_book

        let vResponse =
            refreshAnalysts.View.MapAsync(function
                | book -> 
                    Console.Log("refreshAnalysts.View.MapAsync book: ", book)
                    async { 
                        return! Server.ReadAnalysts book })


        div [on.afterRender (fun _ -> refreshAnalistView initial_book)] [
            h2 [] [text "Oil Analysts and Supervisors"]
            vResponse
            |> View.WithInitOption
            |> Doc.BindView (function
                | None -> 
                    Doc.Concat [
                    img [attr.src @"Content/transparent-blue-loading-image-gif-5.gif"; attr.style "width:250px;height:250px;"] []
                    text "Please wait, db retrieval is running..." ]
                | Some response ->
                    match response with
                    | AnalystResponse (ret_book, analysts) -> tableAnalysts ret_book analysts // inital_book isn't updated: view is responsive, no page reload
                    | AnalystError error -> p [] [text error]
                )
        ]
    type ImportSelection = {mutable book: string; mutable fromAsOf: string; mutable toAsOf: string; }
    let importSel = {book = ""; fromAsOf = initAsofFrom; toAsOf = initAsofTo; }
    let AsofFrom_Changed value =  
        importSel.fromAsOf <- value
    let AsofTo_Changed value = 
        importSel.toAsOf <- value
    let importIsRunning = Var.Create false
    let importLabel = Var.Create ""
    let runImport (isMulti: bool) = 
        async {
            Var.Set importLabel ""
            Var.Set importIsRunning true
            if isMulti then
                let! resp = Server.MultiImport importSel.book importSel.fromAsOf importSel.toAsOf
                match resp with
                | ImportOkResponse (book, range) ->
                    Var.Set importLabel ("Multi-import ok for book " + book + "  range " + range)
                | ImportError err ->
                    Var.Set importLabel ("Multi-import error: " + err)
            else 
                let! resp = Server.SingleImport importSel.book importSel.fromAsOf
                match resp with
                | ImportOkResponse (book, asof) ->
                    Var.Set importLabel ("Single-import ok for book " + book + "  asof " + asof)
                | ImportError err ->
                    Var.Set importLabel ("Single-import error: " + err)
            Var.Set importIsRunning false
            refreshLogViewFromSelection None
        } |> Async.StartImmediate
    let runMultiImport (el:Dom.Element) (ev:Dom.Event) = 
        ev.PreventDefault()
        runImport true
    let runSingleImport (el:Dom.Element) (ev:Dom.Event) = 
        ev.PreventDefault()
        runImport false
    let RetrieveLogs (initial_book:string) =
        Console.Log("RetrieveLogs book: ", initial_book)
        SetInitBookCo initial_book
        importSel.book <- initial_book
        let vResponse =
            refreshLogs.View.MapAsync(function
                | current_combined_log -> 
                    let curr_log_selected = current_combined_log.SelectedLog
                    let curr_book = current_combined_log.Book
                    match curr_log_selected with
                    | None -> 
                        Console.Log("refreshLogs.View.MapAsync book: ", curr_book)
                        async { 
                            return! Server.ReadLogs curr_book }
                    | Some asof ->
                        let date_str = asof.ToShortDateString()
                        Console.Log("refreshLogs.View.MapAsync as-of " + date_str + " from date: ", asof)
                        async { 
                            return! Server.ReadLogDetails curr_book date_str }
            )
        div [
                on.afterRender (fun _ -> 
                    datePicker "#DateFrom" dateStdFormat AsofFrom_Changed initAsofFrom
                    datePicker "#DateTo" dateStdFormat AsofTo_Changed initAsofTo
                    refreshLogViewFromBook initial_book
                    ); ] [
            h2 [] [text "Run as-of import"]
            div [] [
                span [attr.style "width:153px; height: 50px; float: left;"] [text "As-of Date Range"]
                span [attr.style "width:50px; float: left; "] [text "From"]
                input [attr.id "DateFrom"; attr.size "10"; attr.``type`` "text"] []
                label [attr.style "margin-left: 60px; width: 50px; display: inline-block; font-weight: normal"] [text "To"]
                input [attr.id "DateTo"; attr.size "10"; attr.``type`` "text"] []
            ]
            div [attr.style "clear: both; padding-bottom: 5px;"] [
                importIsRunning.View
                |> Doc.BindView ( function
                    | false -> Doc.Concat [
                        button [ attr.``class`` "btn btn-warning btn-outline"; 
                                attr.style "font-size:14px; color:white;"; on.click runSingleImport;] 
                            [text "Start Single Date Import"]
                        button [ attr.``class`` "btn btn-danger btn-outline"; 
                                attr.style "font-size:14px; color:white;"; on.click runMultiImport] 
                            [text "Start Multi Date Import"]
                        ]
                    | true -> Doc.Concat [
                        img [attr.src @"Content/transparent-blue-loading-image-gif-5.gif"; attr.style "width:250px;height:250px;"] []
                        text "Please wait, as-of import is running..." 
                        ]
                )
                br [] []
                label [] [textView importLabel.View]
                br [] []
            ]
            h2 [] [text "Log history"]
            vResponse
            |> View.WithInitOption
            |> Doc.BindView (function
                | None -> 
                    Doc.Concat [
                    img [attr.src @"Content/transparent-blue-loading-image-gif-5.gif"; attr.style "width:250px;height:250px;"] []
                    text "Please wait, db retrieval is running..." ]
                | Some response ->
                    match response with
                    | LogResponse (resp_book, logs) -> 
                        importSel.book <- resp_book
                        tableLogs resp_book logs // inital_book isn't updated: view is responsive, no page reload
                    | LogDetails (resp_book, asof, lines) -> logDetails resp_book asof lines
                    | LogError error -> p [] [text error]
                )
        ]
    
    let RetrieveTrades (sel_cargo_id: SqlDB.ParsedTrade) (sel_alert_type:string) (bookCo:string) = 
        JS.Inline "Object.defineProperties(Array.prototype, 
            {
                shuffle : { enumerable : false }, 
                clone : { enumerable : false }, 
                unique : { enumerable : false }, 
                from : { enumerable : false }, 
                contains : { enumerable : false }, 
            })" |> ignore
        0m |> ignore
        
        SetInitBookCo bookCo

        match sel_cargo_id with
        | SqlDB.NoSelection ->
            Console.Log("sel_cargo_id: no selection")
        | SqlDB.IntSel i -> 
            Console.Log("sel_cargo_id: " + string i)
            htmlInput.Set (string i)
            rvInput.Update (fun curr -> { curr with input = string i; entity_type = sel_alert_type})

        let vResponse =
            submit.View.MapAsync(function
                | None -> async { return  NoInput "Ready to start"}
                | Some data -> 
                    async { 
                        try
                            let! received = Server.RetrieveOilData data.book data.entity_type data.input
                            return Received received
                        with | e -> 
                            return GeneralError ("Error: " + e.Message)
                    }
            )
        
        Doc.Concat [
        h2 [] [text "Oil Data Selection"]
        p [] [text "You can retrieve trades/cargoes/costs (max 90) from DB by trade/cargo/cost/cpty number, customize (hide/move/sort) the columns and search within the retrieved results"]
        form [] [
                Doc.Input [attr.id "retrieve_input"; attr.``type`` "number"; attr.``class`` "mt-1"; attr.``data-`` "role" "input" ; // attr.``data-`` "clear-button" "false";
                attr.``data-`` "prepend" "Retrieve data " ; attr.placeholder " by trade, cargo, cost or cpty num"; on.change (fun _ _ -> 
                    htmlSubmit.Trigger()) ] htmlInput
                Doc.Button "Retrieve from DB" [attr.``class`` "button success"] 
                    htmlSubmit.Trigger
                span [attr.style "display: inline-block;width:auto; margin-left:15px; margin-right:12px;"] [text "by";]
                select [attr.id SearchByDropDown; attr.style "display: inline-block;width:auto;font-size:14px;line-height:normal;height:22px"; 
                        on.change <@ fun el ev ->
                            let searchBySelect = JQuery.Of("#"+SearchByDropDown)
                            let sel_alert_type  = string (searchBySelect.Val())
                            rvInput.Update (fun curr -> { curr with entity_type = sel_alert_type})
                            htmlSubmit.Trigger()
                        @>
                        on.afterRender 
                            <@ fun el -> 
                                let searchBySelect = JQuery.Of("#"+SearchByDropDown)
                                Console.Log("sel_alert_type: ", sel_alert_type)
                                Console.Log("searchBySelect: ", searchBySelect)
                                match sel_alert_type with
                                | x when x = ServerModel.ByCpty -> searchBySelect.Val(ServerModel.ByCpty).Ignore
                                | x when x = ServerModel.ByTrade -> searchBySelect.Val(ServerModel.ByTrade).Ignore
                                | x when x = ServerModel.ByFee -> searchBySelect.Val(ServerModel.ByFee).Ignore
                                | _ -> searchBySelect.Val(ServerModel.ByCargo).Ignore
                            @>;
                        ] [     
                    Tags.option [attr.value ServerModel.ByCargo;] [ text "Cargo Id" ]; 
                    Tags.option [attr.value ServerModel.ByTrade] [ text "Deal num"]
                    Tags.option [attr.value ServerModel.ByCpty] [ text "Cpty num"]
                    Tags.option [attr.value ServerModel.ByFee] [ text "Fee Id"] ]
                ]
        Doc.BindView (fun l -> Doc.Empty) ignoreView
        vResponse
        |> View.WithInitOption
        |> Doc.BindView (function
            | None -> 
                //<img src="Content/transparent-blue-loading-image-gif-5.gif" style="width:250px;height:250px;"   />
                Doc.Concat [
                img [attr.src @"Content/transparent-blue-loading-image-gif-5.gif"; attr.style "width:250px;height:250px;"] []
                text "Please wait, db retrieval is running..." ]
            | Some response -> TableRetrieve response
            )
        ]


    let SetCompany (el:Dom.Element) (ev:Dom.Event) = 
        async {
        let! optLabel = GetBookCo()
        Console.Log(optLabel)
        let! before = Server.GetBookCompany()
        Console.Log("before: " + before)
        let! check_list = Server.SetBookCompany(optLabel)
        check_list |> Seq.iter Console.Log
        let! afterSession = Server.GetBookCompany()
        Console.Log("after: " + afterSession) 
        rvInput.Update (fun curr -> {curr with book = optLabel})
        submit.Trigger()
        alertSelVar.Update (fun curr -> {curr with book = optLabel})
        refreshAlerts.Trigger()
        refreshAnalistView optLabel
        refreshLogViewFromBook optLabel
        auditInput.Set NoAlertSelection
        auditSubmit.Trigger()
        } |> Async.StartImmediate
    
    let SetInitSession (el:Dom.Element) =
        async {
            let! assigned2me = Server.GetAssignedAlerts()
            updateAssignedBadges assigned2me
        } |> Async.StartImmediate
    
    let SelectCompany (bookCo:string) =
        Console.Log("Select Company book from server: " + bookCo)
        let spaIsSel, incIsSel   = 
            match bookCo with
            | str when str = EtsInc -> 
                [attr.id EtsSpA],[attr.id EtsInc;attr.selected ""]
            | _ -> 
                [attr.id EtsSpA; attr.selected ""], [attr.id EtsInc]
        select [attr.id DropDownList; attr.style "width:auto;font-size:14px;line-height:normal;height:22px"; 
                on.change SetCompany; on.afterRender SetInitSession] [     
            Tags.option spaIsSel [ text EtsSpA ]; 
            Tags.option incIsSel [ text EtsInc]
        ]
           