namespace ClientServerTable

open WebSharper
open WebSharper.Sitelets
open WebSharper.UI
open WebSharper.UI.Server
open WebSharper.UI.Html
open System
open System.Configuration
open System.Web
open SqlLib
open SqlDB
open Server

type AnimateStyle() =
    inherit Resources.BaseResource("MyScripts", "animate.min.css")

type OverlayStyle() =
    inherit Resources.BaseResource("MyScripts", "overlay_hover.css") 

type MetroStyle() =
    inherit Resources.BaseResource("MyScripts", "metro-4.2.48/css/metro-all.min.css") 
    //"https://cdn.metroui.org.ua", "v4.2.47/css/metro-all.min.css") //"MyScripts", "metro/metro-all.min.css") 

[<Require(typeof<JQuery.Resources.JQuery>)>]
type MasterScript() =
    inherit Resources.BaseResource("MyScripts","bootstrap/js/bootstrap.min.js","tss_master.js")

type TssScript() =
    inherit Resources.BaseResource("MyScripts","tss_master.js")

type FontAwesomeStyle() =
    inherit Resources.BaseResource("https://use.fontawesome.com", "releases/v5.8.2/css/all.css") 

//[<Require(typeof<FontAwesomeStyle>)>]
//type BootstrapStyle() =
//    inherit Resources.BaseResource("https://maxcdn.bootstrapcdn.com","bootstrap/3.3.4/css/bootstrap.min.css") //"MyScripts", "mdb/css/bootstrap.min.css" 

[<Require(typeof<FontAwesomeStyle>)>]
type MDBStyle() =
    inherit Resources.BaseResource("MyScripts", "mdb/css/addons/datatables.min.css") 

[<Require(typeof<MDBStyle>)>]
type DataTableStyle() =
    inherit Resources.BaseResource("MyScripts", "mdb/css/mdb.min.css") 

type PivotStyle() =
    inherit Resources.BaseResource("MyScripts", "pivot/pivot.min.css") 

[<Require(typeof<JQuery.Resources.JQuery>)>]
type MetroScript() =
    inherit Resources.BaseResource("MyScripts", "metro-4.2.48/js/metro.min.js")
        //"https://cdn.metroui.org.ua", "v4.2.47/js/metro.min.js") //"MyScripts", "metro/metro.min.js") // "MyScripts/metro.js"  

type JTableStyle() =
    inherit Resources.BaseResource("Scripts",
        "jtable/themes/metro/blue/jtable.min.css")

type JQueryUIStyle() =
    inherit Resources.BaseResource("Scripts",
        "jquery-ui-1.12.1/jquery-ui.min.css") //  jqueryui/themes/base/minified/

type ChosenStyle() =
    inherit Resources.BaseResource("MyScripts",
        "chosen/chosen.css")

[<Require(typeof<JQuery.Resources.JQuery>)>]
type JQueryUIScript() =
    inherit Resources.BaseResource("Scripts",
        "jquery-ui-1.12.1/jquery-ui.js") //"jquery-ui-1.12.1.min.js jquery-ui-1.10.4/ui/minified/jquery-ui.min.js"

[<Require(typeof<JQueryUIScript>)>]
type JTableScript() =
    inherit Resources.BaseResource("Scripts",
        "jtable/jquery.jtable.js")

[<Require(typeof<JQueryUIScript>)>]
type ChosenScript() =    
    inherit Resources.BaseResource("MyScripts",
        "chosen/chosen.jquery.min.js") 

[<Require(typeof<JQuery.Resources.JQuery>)>]
type PopperScript() =
    inherit Resources.BaseResource("MyScripts",
        "mdb/js/popper.min.js")  
        
[<Require(typeof<PopperScript>)>]
type BootstrapScript() = 
    inherit Resources.BaseResource("MyScripts",
        "mdb/js/bootstrap.min.js")
       
[<Require(typeof<BootstrapScript>)>]
type MDBScript() = 
    inherit Resources.BaseResource("MyScripts",
        "mdb/js/mdb.min.js")

[<Require(typeof<MDBScript>)>]
type DataTableScript() = 
    inherit Resources.BaseResource("MyScripts",
        "mdb/js/addons/datatables.min.js")


[<Require(typeof<JQueryUIScript>)>]
type PlotlyScript() =
    inherit Resources.BaseResource("MyScripts",
        "pivot/plotly-basic-latest.min.js")

[<Require(typeof<PlotlyScript>)>]
type PivotScript() =
    inherit Resources.BaseResource("MyScripts",
        "pivot/pivot.min.js")

[<Require(typeof<PivotScript>)>]
type RenderersScript() =
    inherit Resources.BaseResource("MyScripts",
        "pivot/plotly_renderers.js")


module Templating =

    type MainTemplate = Templating.Template<"Main.html">
    type TssTemplate = Templating.Template<"tss_template.html">

    // Compute a menubar where the menu item for the given endpoint is active
    let MenuBar (ctx: Context<EndPoint>) endpoint : Doc list =
        let ( => ) txt act =
             li [if endpoint = act then yield attr.``class`` "active"] [
                a [attr.href (ctx.Link act)] [text txt]
             ]
        [
            "Home" => EndPoint.Home
            "About" => EndPoint.About
            "Oil Alerts" => EndPoint.Alerts
            "Data" => EndPoint.Table 
            "Admin" => EndPoint.Admin
            "Log" => EndPoint.Log
            "Pivot" => EndPoint.Pivot
        ]

    let Main ctx action (title: string) (body: Doc list) =
        Content.Page(
            MainTemplate()
                .Title(title)
                .MenuBar(MenuBar ctx action)
                .Body(body)
                .Doc()
        )
    



    let Tss (ctx:Context<EndPoint>) (title: string) (body: Doc list) (badge: Doc) (company: Doc) =
        Content.Page(
            TssTemplate() 
                //.MyApp("/reporting_oil_fsharp")
                .Title(title)
                .HeaderColor("background-color: " + ConfigurationManager.AppSettings.["HeaderColor"])
                .EnvMode("TSS Reporting " + ConfigurationManager.AppSettings.["Mode"])
                .UserTag(" - " + GetCurrentUser())
                .Badge(badge)
                .BookCo(company)
                .Body(body)
                .Doc()
        ) 




module Site =
    open WebSharper.UI.Html
    open LinqToDB
    open System.Reflection
    open Import2TSS
    open WebSharper.UI.Html
    open ServerModel

    let getUserName () =
        let currentUser = GetCurrentUser ()
        let currentUserName = (currentUser.Split('\\') |> Array.last).ToLower()
        currentUserName

    let db = new DB()
    let bookCo () = 
        let userID = getUserName()
        if (db.userIdOfBookCompany Import2TSS.Counterparty.ETS userID) then
            ServerModel.EtsSpA
        else 
            ServerModel.EtsInc
    
    let SelectCompany bookCo = 
        //let bookCo = bookCo() //  the client quotation can only contain either JavaScript globals or local variables
        div [][ client <@ Client.SelectCompany bookCo @>] 
    
    let ShowBadges =
        client <@ ClientBase.ShowAssignedBadges() @>

    let AlertsPage ctx =
        let bookCo = bookCo()  //  the client quotation can only contain either JavaScript globals or local variables
        let codes =
            Enum.GetValues(typedefof<Alerting.AlertCodes>)
            |> Seq.cast<Alerting.AlertCodes>
            |> Seq.map (fun c -> c.GetEnumDescription())
            |> Seq.toArray
        let statuses =
            Alerting.AlertStatuses
        Templating.Tss ctx "Oil Alerts" [
            Doc.WebControl(new Web.Require<AnimateStyle>())
            Doc.WebControl(new Web.Require<JQueryUIStyle>())
            Doc.WebControl(new Web.Require<JTableStyle>())
            Doc.WebControl(new Web.Require<ChosenStyle>())
            Doc.WebControl(new Web.Require<JQueryUIScript>())
            Doc.WebControl(new Web.Require<JTableScript>())
            Doc.WebControl(new Web.Require<ChosenScript>())
            client <@ Client.ResponsiveAlerts bookCo codes statuses @>
            Doc.WebControl (new Web.Require<MasterScript>())
        ] ShowBadges (SelectCompany bookCo)

    let AdminPage ctx =
        let bookCo = bookCo() 
        Templating.Tss ctx "Oil Admin" [
            Doc.WebControl (new Web.Require<DataTableStyle>())
            div [] [client <@ Client.RetrieveAnalysts bookCo  @>]
            Doc.WebControl (new Web.Require<DataTableScript>())
         ] ShowBadges (SelectCompany bookCo)
     
    let LogPage ctx =
        let bookCo = bookCo() 
        Templating.Tss ctx "Oil Log" [
            Doc.WebControl(new Web.Require<JQueryUIStyle>())
            Doc.WebControl(new Web.Require<JQueryUIScript>())
            Doc.WebControl (new Web.Require<DataTableStyle>())
            h2 [] [text "Oil Log"]
            div [] [client <@ Client.RetrieveLogs bookCo  @>]
            Doc.WebControl (new Web.Require<DataTableScript>())
         ] ShowBadges (SelectCompany bookCo)
         
    let PivotPage (ctx:Context<EndPoint>) =
        let bookCo = bookCo() 
        Templating.Tss ctx "Alert Pivot" [
            Doc.WebControl(new Web.Require<JQueryUIStyle>())
            Doc.WebControl (new Web.Require<PivotStyle>())
            div [] [client <@ Client.RetrievePivot bookCo @>]
            Doc.WebControl (new Web.Require<RenderersScript>())
            Doc.WebControl (new Web.Require<MasterScript>())
         ] ShowBadges (SelectCompany bookCo)

    let HomePage (ctx:Context<EndPoint>) =
        let  table_url = ctx.Link EndPoint.Table
        Templating.Main ctx EndPoint.Home "Home" [
            h1 [] [text "Say Hi to the server!"]
            div [] [client <@ Client.Main table_url  @>]
        ]

    let AboutPage ctx =
        Templating.Main ctx EndPoint.About "About" [
            h1 [] [text "About"]
            p [] [text "This is a template WebSharper client-server application."]
        ]
    

    let TablePage ctx =
        let sel_alert_type : string = string HttpContext.Current.Request.Form.[SelAlertType]
        let sel_cargo_id : string = string HttpContext.Current.Request.Form.[SelAlertKey]
        let bookCo = bookCo()  //  the client quotation can only contain either JavaScript globals or local variables
        let checked_cargo_id = 
            match sel_cargo_id with
            | Server.Valid sel-> sel
            | _ -> SqlDB.ParsedTrade.NoSelection

        Templating.Tss ctx "Oil Data" [
            Doc.WebControl (new Web.Require<MetroStyle>())
            Doc.WebControl (new Web.Require<OverlayStyle>())
            div [] [client <@ Client.RetrieveTrades checked_cargo_id sel_alert_type bookCo @>]
            Doc.WebControl (new Web.Require<MasterScript>())
            Doc.WebControl (new Web.Require<MetroScript>())
        ] ShowBadges (SelectCompany bookCo)

    [<Website>]
    let Main =
        Application.MultiPage (fun ctx endpoint ->
            match endpoint with
            | EndPoint.Home 
            | EndPoint.OldAlerts -> Content.RedirectPermanent EndPoint.Alerts // HomePage ctx
            | EndPoint.About -> AboutPage ctx
            | EndPoint.Table -> TablePage ctx
            | EndPoint.Alerts -> AlertsPage ctx
            | EndPoint.Admin -> AdminPage ctx
            | EndPoint.Log -> LogPage ctx
            | EndPoint.Pivot -> PivotPage ctx
            | EndPoint.Download -> 
                let currentUserName = getUserName()
                let fileName = String.Format(@"Excel/OilReport_{0}.xlsx", currentUserName)
                Content.File(fileName, ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")
                |> Content.WithHeader "Content-Disposition" (sprintf "attachment; filename=%s" fileName)
        )
