namespace ClientServerTable

open WebSharper
open WebSharper.JavaScript
open WebSharper.UI
open WebSharper.UI.Html
open Server
open WebSharper.JQuery
open ClientBase
open SqlLib

[<JavaScript>]
module ARecs =

    let JSPivot (alerts: obj[]) =
            JS.Inline(@"
                        var derivers = $.pivotUtilities.derivers;
                        var renderers = $.extend($.pivotUtilities.renderers,
                            $.pivotUtilities.plotly_renderers);
                        $('#AlertPivot').pivotUI(
                                $0.Records
                                ,{
                                    rows: ['AlertCode'],
                                    cols: ['Status','Month'],
                                    rendererName: 'Heatmap',renderers: renderers
                                },
                                overwrite = true
                            );            
            ", {|Records = alerts|})

    let GetItemsAsync (data: Server.JTableData, param: JTableParams) = 
        async {
            let! alertPage = Server.GetOilAlertsAsync (data.bookcompany, data.user, data.code, data.status, data.key, data.closed, data.dateFrom, data.dateTo, param.jtStartIndex, param.jtPageSize, param.jtSorting) 
            return {| Message = alertPage.Message; Records = alertPage.Records |> Array.map addHiddenKey ; Result = alertPage.Result; TotalRecordCount = alertPage.TotalRecordCount |}
        }
        |> DeferredOfAsync

    let GetOilAuditAsync  (data: AuditData, param: JTableParams)   = 
        async {
            Console.Log("GetOilAuditAsync")
            Console.Log(data)
            let! auditPage = Server.GetOilAuditAsync (data.alertCode, data.alertKey, data.bookCo, param.jtStartIndex, param.jtPageSize)  
            return 
                {| 
                    Message = auditPage.Message; 
                    Records = auditPage.Records |> Array.map Json.Deserialize ; 
                    Result = auditPage.Result; 
                    TotalRecordCount = auditPage.TotalRecordCount; 
                |}
        }  
        |> DeferredOfAsync


    let populateAuditTable (detail: AuditData)  (analysts: SqlDB.Analyst[]) =
        let analystOptions  = analysts2options analysts
        Console.Log("populateAuditTable starts!")
        let auditTable = JQuery.Of("#AuditTable")
        Console.Log(auditTable)
        JS.Inline("$0.jtable($1)", 
            auditTable,     
            {|
                paging = true; pageSize = 10; saveUserPreferences = false; pageList = "minimal"; 
                sorting = false;
                actions = 
                    {|
                        listAction = FuncWithArgs GetOilAuditAsync
                    |};
                fields = 
                 {|
                    TransactionDate = 
                        {|
                            title = "Transaction Date";
                            edit = false;
                            display = fun data -> dateFormat data?record?TransactionDate DateAndTime;
                        |};
                    AlertCode = 
                        {|
                            title = "Code";
                            list = true;
                        |};
                    AlertKey = 
                        {|
                            title = "Key";
                            list = true;
                        |};
                    Status = 
                        {|
                            title = "Status";
                            list = true;
                        |};
                    CreationDate = 
                        {|
                            title = "Creation Date";
                            edit = false;
                            display = fun data -> dateFormat data?record?CreationDate DateOnly;
                        |};
                    Message = 
                        {|
                            title = "Message";
                            list = true;
                        |};
                    AssignedTo = 
                        {|
                            title = "Assigned To";
                            list = true;
                            options = analystOptions;
                        |};
                    Note = 
                        {|
                            title = "Note";
                            list = true;
                        |};
                    Outcome = 
                        {|
                            title = "Outcome";
                            list = true;
                        |};
                 |} 
            |}
        )
        JS.Inline("$0.jtable('load', $1)", 
            auditTable, 
            {|
                alertKey = detail.alertKey;
                alertCode = detail.alertCode;
                bookCo = detail.bookCo;
            |}
        )

    
    let AlertUpdate (queryString: string) : Async<UpdateResponse> = 
        async {
            Console.Log <| "updateAction - query string:" + queryString
            let oilAlert =  QueryString2Json (queryString)
            Console.Log <| "CompositeKey: " + oilAlert?CompositeKey
            let compositeKey  = oilAlert?CompositeKey
            let alertJson = JS.Inline("JSON.stringify($0)", oilAlert) 
            try
                let! bookCo = GetBookCo ()
                let! resp = Server.UpdateOilAlertAsync alertJson bookCo compositeKey

                if (resp.Result = "OK") then
                    Console.Log <| "Server ok message: " + resp.Message
                    RefreshHistory oilAlert bookCo 
                Console.Log resp.Message
                return resp
            with 
            | exc -> return {Result="ERROR"; Message=exc.Message;}
        }  
    let mutable lastSel : obj array = [||]
    let populateTable(response: AlertSelection) (codes: string array) (statuses: string array) (analysts: SqlDB.Analyst[]) =
        let analystOptions  = analysts2options analysts
        let jstable = JQuery.Of("#AlertTable")
        JS.Inline(
                            "$0.jtable($1)", 
                            jstable, 
                            {| 
                                title = "Oil Alerts"; paging = true; pageSize = 10; saveUserPreferences = false; pageList = "minimal"; 
                                sorting = true; defaultSorting = "AlertKey ASC, CreationDate ASC, AlertCode ASC"; multiselect = false; selecting = true;
                                actions = 
                                    {|
                                        listAction =  FuncWithArgs GetItemsAsync; 
                                        updateAction = AlertUpdate >> DeferredOfAsync;
                                        createAction = CreateManualAlertAsync;
                                    |};
                                fields  = 
                                    New [
                                        "Commodity" => 
                                            {|
                                                title = "Commodity";
                                                list = true;
                                                sorting = true;
                                                edit = true;
                                                create = true;
                                            |};
                                        "Portfolio" => 
                                            {|
                                                title = "Portfolio";
                                                list = true;
                                                sorting = true;
                                                edit = true;
                                                create = true;
                                            |};
                                        "AlertCode" => 
                                            {|
                                                title = "Code";
                                                list = true;
                                                sorting = true;
                                                edit = true;
                                                ``type`` = "combobox";
                                                inputClass = "btn btn-primary dropdown-toggle";
                                                options = codes;
                                                create = true;
                                            |};
                                        "AlertKey" => 
                                            {|
                                                title = "Key";
                                                list = true;
                                                sorting = true;
                                                edit = true;
                                                create = true;
                                                display = fun data -> 
                                                    ("sel_cargo_id", data?record?AlertKey)  |==> EndPoint.Table 
                                                    |> ToHtml
                                            |};
                                        "CompositeKey" => 
                                            {|
                                                list = false;
                                                key = true;
                                                edit = false;
                                                create = false;
                                            |};
                                        "Status" => 
                                            {|
                                                title = "Status";
                                                list = true;
                                                sorting = true;
                                                edit = true;
                                                ``type`` = "combobox";
                                                inputClass = "btn btn-primary dropdown-toggle";
                                                options = statuses;
                                                create = false;
                                            |};
                                        "LastTransactionDate" =>
                                            {|
                                                title = "Last Transaction";
                                                edit = false;
                                                sorting = true;
                                                create = false;
                                                display = fun data -> dateFormat data?record?LastTransactionDate DateAndTime;
                                            |};
                                        "CreationDate" => 
                                            {|
                                                title = "Creation Date";
                                                edit = false;
                                                sorting = true;
                                                create = false;
                                                display = fun data -> dateFormat data?record?CreationDate DateOnly;
                                            |};
                                        "Message" => 
                                            {|
                                                title = "Message";
                                                list = true;
                                                sorting = true;
                                                edit = true;
                                                ``type`` = "textarea";
                                                create = true;
                                            |};
                                        "AssignedTo" => 
                                            {|
                                                title = "Assigned To";
                                                list = true;
                                                sorting = true;
                                                edit = true;
                                                inputClass = "btn btn-primary dropdown-toggle";
                                                create = false;
                                                options = analystOptions;
                                            |};
                                        "Note" => 
                                            {|
                                                title = "Note";
                                                list = true;
                                                sorting = true;
                                                edit = true;
                                                ``type`` = "textarea";
                                                create = true;
                                            |};
                                        "Outcome" => 
                                            {|
                                                title = "Outcome";
                                                list = true;
                                                sorting = true;
                                                edit = true;
                                                ``type`` = "textarea";
                                                create = true;
                                            |}                        
                                    ]   ;
                                selectionChanged = FuncWithArgs (fun (ev, dt) -> 
                                    Console.Log("SelectionChanged")
                                    colourTable lastSel
                                    auditInput.Set(NoAlertSelection)
                                    auditSubmit.Trigger()
                                    let table = JQuery.Of("#AlertTable")
                                    let selectedRows = JS.Inline("$0.jtable('selectedRows')", table)
                                    JQuery.Each (selectedRows, (fun name selRow ->
                                        let JQselRow = JQuery.Of(selRow)
                                        let record = JQselRow?data("record")
                                        Console.Log(record)
                                        let alertCodeAud = record?AlertCode
                                        let alertKeyAud = record?AlertKey
                                        AlertSelection {alertCode = alertCodeAud; 
                                            alertKey = alertKeyAud; bookCo = alertSelVar.Value.book}
                                        |> auditInput.Set 
                                        auditSubmit.Trigger()
                                    ) )  |> ignore   );
                                rowUpdated  =  FuncWithArgs (fun (ev, dt) ->
                                    colourTable lastSel
                                );
                                recordsLoaded  =  FuncWithArgs (fun (ev, dt) ->
                                    lastSel <- dt
                                    colourTable dt
                                );
                             |};
        )
        JS.Inline("$0.jtable('load',$1)", jstable, 
            sel2jtable response)
