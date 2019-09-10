namespace SqlLib

open Import2TSS
open FSharp.Data.Sql
open System
open System.Data
open System.Configuration

open FSharp.Data.Sql.Providers

module SqlDB =

    // aside from project properties for fsc build, you may need --define:OFFLINE in tools>options>f# tools> f# interactive (restart fsi) 
    #if OFFLINE 
    [<Literal>]
    let resolutionPath =  __SOURCE_DIRECTORY__ +  @"\bin\debug"
    [<Literal>]
    let myConnStr =  @"Data Source=" +   __SOURCE_DIRECTORY__ + @"/offline/offline_db.s3db;Version=3;" 
    type Sql = SqlDataProvider<
                DatabaseVendor = Common.DatabaseProviderTypes.SQLITE, 
                ConnectionString = myConnStr,
                ResolutionPath = resolutionPath // System.Data.SQLite.dll
                >
    #else
    [<Literal>]
    let mySchemaPath =
        __SOURCE_DIRECTORY__ + @"\.sqlserver.schema" // fixed misspelt .\
    [<Literal>]
    let myConnStr =   @"Data Source=;Initial Catalog=;User ID=;Password="
    type Sql = SqlDataProvider<
                DatabaseVendor = Common.DatabaseProviderTypes.MSSQLSERVER, 
                // ContextSchemaPath = mySchemaPath, // file created the 1st time you build the proj with the schema path
                // and ctx.SaveContextSchema() just compiled in the code
                // but then you need to comment it to put the provider online again
                ConnectionString = myConnStr
                >

    #endif
    #if INTERACTIVE
    FSharp.Data.Sql.Common.QueryEvents.SqlQueryEvent 
    |> Event.add (fun e -> 
        printfn  "Executing SQL: %s" (e.ToRawSql()))
    #endif

    let AppDB = "TSS_DB"
    #if INTERACTIVE 
    #if OFFLINE 
    let context = Sql.GetDataContext(myConnStr, resolutionPath)
    #else
    let context = Sql.GetDataContext(myConnStr)
    #endif
    #else
    #if OFFLINE 
    let context = Sql.GetDataContext(myConnStr, resolutionPath)
    #else
    let context = Sql.GetDataContext(ConfigurationManager.ConnectionStrings.[AppDB].ConnectionString)
    #endif
    #endif

    #if OFFLINE
    let analystsTable = context.Main.Analyst
    #else 
    let analystsTable = context.OilPhysical.Analyst
    #endif
    
    type ExtTrade = {t:TradeValid; n: NominationValid}

    type ParsedTrade = NoSelection | IntSel of int     
    #if OFFLINE 
    // TODO: "only ONLINE, no offline table at the moment"
    #else 
    let tradesQuery (book:string) (sel: ParsedTrade) = 
        
        match sel with
        | NoSelection -> 
            query {
            for t in context.OilPhysical.EndurTradeValid do  
            join n_del in (!!) context.OilPhysical.EndurNominationValid on
                     ((t.DealNumber,t.ParcelGroupId,t.ParcelId) 
                     = (n_del.DeliveryDealNumber,n_del.DeliveryParcelGroup,n_del.DeliveryParcelId))
            join n_rec in (!!) context.OilPhysical.EndurNominationValid on
                     ((t.DealNumber,t.ParcelGroupId,t.ParcelId) 
                     = (n_rec.ReceiptDealNumber,n_rec.ReceiptParcelGroup,n_rec.ReceiptParcelId))
            where (t.BookingCompanyShortName = book)
            take 90
            select (t,n_del.CargoId, n_rec.CargoId)  
            }
        | IntSel i -> 
            query {
            for t in context.OilPhysical.EndurTradeValid do  
            join n_del in (!!) context.OilPhysical.EndurNominationValid on
                     ((t.DealNumber,t.ParcelGroupId,t.ParcelId) 
                     = (n_del.DeliveryDealNumber,n_del.DeliveryParcelGroup,n_del.DeliveryParcelId))
            join n_rec in (!!) context.OilPhysical.EndurNominationValid on
                     ((t.DealNumber,t.ParcelGroupId,t.ParcelId) 
                     = (n_rec.ReceiptDealNumber,n_rec.ReceiptParcelGroup,n_rec.ReceiptParcelId))
            where (t.BookingCompanyShortName = book && (t.DealNumber = i || n_del.CargoId = i || n_rec.CargoId = i || t.ExternalLegalEntityId = (string i)))
            take 90
            select (t,n_del.CargoId, n_rec.CargoId)  
            }
    
    let nominationsQuery (book:string) (sel: ParsedTrade) =
        match sel with
        | NoSelection -> 
            query {
                for n in context.OilPhysical.EndurNominationValid do
                where (n.BookingCompany = book)
                take 90
                sortByDescending n.TitleTranfertDate
                select n
            }
        | IntSel i -> 
            query {
                for n in context.OilPhysical.EndurNominationValid do
                where (n.BookingCompany = book && (
                        n.DeliveryExternalLegalEntityId = i ||
                        n.CargoId = i ||
                        n.DeliveryDealNumber = i ||
                        n.ReceiptDealNumber = i
                    ))
                take 90
                sortByDescending n.TitleTranfertDate
                select n
            }
    
    let costsQuery (book:string) (sel: ParsedTrade) =
        let delete_type = Cost.Operations.Delete.GetEnumDescription()
        match sel with
        | NoSelection -> 
            query {
                for c in context.OilPhysical.EndurCost do
                join n in context.OilPhysical.EndurNominationValid
                    // fix with latest version (1.1.68) of SQLProvider https://github.com/fsprojects/SQLProvider/issues/634#issuecomment-529852061
                    on ( (c.CargoId, c.DeliveryId, c.DealNumber) = (n.CargoId, n.DeliveryId, if n.DeliveryDealNumber>0 then n.DeliveryDealNumber else n.ReceiptDealNumber) )
                where (c.BookingCompany = book &&
                    c.FeeStatus <> Cost.ClosedFeeStatus &&
                    c.FeeType <> delete_type
                    )
                take 90
                select c
            }
        | IntSel i -> 
            let i_str = string i
            query {
                for c in context.OilPhysical.EndurCost do
                join n in context.OilPhysical.EndurNominationValid
                    // fix with latest version (1.1.68) of SQLProvider https://github.com/fsprojects/SQLProvider/issues/634#issuecomment-529852061
                    on ( (c.CargoId, c.DeliveryId, c.DealNumber) = (n.CargoId, n.DeliveryId, if n.DeliveryDealNumber>0 then n.DeliveryDealNumber else n.ReceiptDealNumber) )
                where (c.BookingCompany = book &&
                    c.FeeStatus <> Cost.ClosedFeeStatus &&
                    c.FeeType <> delete_type &&
                        ( c.CounterpartyId = i_str ||
                          c.FeeId = i || c.CargoId = i )
                    )
                take 90
                select c
            }


    #endif 
    
    type BookLog = {AsofDate: DateTime; BookCompany: string; 
        StartDate:DateTime; EndDate: DateTime; Status: string }

    let evalLogStatus = function
        | i when i = 2 -> "Success"
        | i when i = 0 -> "Working"
        | _ -> "Fail" //should be 1
        
    #if OFFLINE 
    // TODO: "only ONLINE, no offline table at the moment"
    #else 
    let ReadImportLog (book:string) =
        query {
            for l in context.OilPhysical.ImportBookLog do
            where (l.BookCompany = book)
            sortByDescending l.AsofDate 
            select { AsofDate = l.AsofDate; BookCompany = l.BookCompany; 
                StartDate = l.StartDate; EndDate = l.EndDate; 
                Status = evalLogStatus l.Status}
        }
    #endif

    type LogLine = { Num: int; Text: string}
    #if OFFLINE 
    // TODO: "only ONLINE, no offline table at the moment"
    #else 
    let ReadLogRow (book:string) (asof: DateTime)  =
        query {
            for r in context.OilPhysical.ImportBookLogRow do
            where (r.BookCompany = book && r.AsofDate = asof)
            sortBy r.RowId
            select { Num = r.RowId; Text = r.Message }
        }
    #endif


    type Analyst = { UserName: string; Name:string; Surname: string}
    let analystsQuery (book:string) =
        query {
        for a in analystsTable do
        where (a.BookCompany = book)
        sortBy a.Surname
        select { UserName = a.UserName; Name = a.Name; Surname = a.Surname }
        }
    let analystInsert (book:string) (analyst:Analyst) =
        let insertMe = analystsTable.Create()
        insertMe.BookCompany <- book
        insertMe.UserName <- analyst.UserName
        insertMe.Name <- analyst.Name
        insertMe.Surname <- analyst.Surname
        context.SubmitUpdates()
    let analystUpdate (book:string) (analyst:Analyst) =
        let foundAnalystMaybe = query {
            for a in analystsTable do
            where (a.BookCompany = book && a.UserName = analyst.UserName)
            select (Some a)
            exactlyOneOrDefault
        }
        match foundAnalystMaybe with
        | Some foundAnalyst -> 
            foundAnalyst.Name <- analyst.Name
            foundAnalyst.Surname <- analyst.Surname
            context.SubmitUpdates()
        | None -> ()
    let analystDelete (book:string) (analyst:Analyst) =
        let foundAnalystMaybe = query {
            for a in analystsTable do
            where (a.BookCompany = book && a.UserName = analyst.UserName)
            select (Some a)
            exactlyOneOrDefault
        }
        match foundAnalystMaybe with
        | Some foundAnalyst -> 
            foundAnalyst.Delete()
            context.SubmitUpdates()
        | None -> ()
    
    type DBTable = (string * obj) array array
    type DB() =
        member x.trades (sel: ParsedTrade) (book: string) : Async<DBTable> = async {
        #if OFFLINE
            printfn "only ONLINE, no offline table at the moment"
            return [||]
        #else
            let! queryRes = tradesQuery book sel |> Array.executeQueryAsync 
            return queryRes
            |> Array.map(fun (t,n_del,n_rec) -> 
                t.ColumnValues
                |> Seq.append ([("Del Cargo ID", n_del :> obj)] 
                |> Seq.append [("Rec Cargo ID", n_rec :> obj)] ) 
                |> Seq.toArray)
        #endif
        } 

        member x.nominations  (sel: ParsedTrade) (book: string) : Async<DBTable> = async {
        #if OFFLINE
            printfn "only ONLINE, no offline table at the moment"
            return [||]
        #else
            let! queryRes = nominationsQuery book sel |> Array.executeQueryAsync  
            return queryRes
            |> Array.map(fun n -> 
                n.ColumnValues
                |> Seq.toArray)
        #endif
        }
        
        member x.costs  (sel: ParsedTrade) (book: string) : Async<DBTable> = async {
        #if OFFLINE
            printfn "only ONLINE, no offline table at the moment"
            return [||]
        #else
            let! queryRes = costsQuery book sel |> Array.executeQueryAsync  
            return queryRes
            |> Array.map(fun n -> 
                n.ColumnValues
                |> Seq.toArray)
        #endif
        }
        
        member x.logsRead (book:string) : BookLog[] =
        #if OFFLINE
            printfn "only ONLINE, no offline table at the moment"
            [||]
        #else
            ReadImportLog book |> Seq.toArray
        #endif

        member x.logDetailsRead (book:string) (asof:DateTime) : LogLine[] =
        #if OFFLINE
            printfn "only ONLINE, no offline table at the moment"
            [||]
        #else
            ReadLogRow book asof |> Seq.toArray
        #endif

        member x.analysts (book:string) =
            analystsQuery book |> Seq.toArray
        member x.analystCreate (book:string) (analyst:Analyst) =
            analystInsert book analyst
        member x.analystUpdate (book:string) (analyst:Analyst) =
            analystUpdate book analyst
        member x.analystDelete (book:string) (analyst:Analyst) =
            analystDelete book analyst


        member x.cache() =
            #if COMPILED 
            printfn "you are in compiled mode! You can set OFFLINE from project properties for fsc build" 
            #else 
            #if INTERACTIVE
            printfn "you are in fsi interactive mode! You may need --define:OFFLINE in tools>options>f# tools> f# interactive (restart fsi)" 
            #else
            printfn "your compiler mode is unknown!"
            #endif
            #endif 

            #if OFFLINE
            printfn "you are offline"
            #else 
            printfn "you are online"
            // file created the 1st time you build the proj with the schema path and ctx.SaveContextSchema() just compiled in the code
            // this seems to be a design/compile time (not runtime) feature
            let result = context.SaveContextSchema()
            printfn "check schema under %s " mySchemaPath
            if (result <> null) then
                printfn "offline-first is ok now!"
                use sw = new System.IO.StreamWriter(mySchemaPath + ".txt", true)
                sw.WriteLine(sprintf "%s %A" (DateTime.Now.ToShortTimeString()) result)
            #endif 



