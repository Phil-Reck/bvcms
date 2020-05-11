﻿-- add giving sources widget
IF (select count(*) from DashboardWidgets where [Name] like 'Recent Attendance Trends' and [System] = 1) = 0
BEGIN
  INSERT INTO [dbo].[Content]
           ([Name],[Title],[Body],[DateCreated],[TypeID],[ThumbID],[RoleID],[OwnerID],[CreatedBy])
     VALUES
           ('WidgetRecentAttendanceTrendsHTML','Edit Text Content',
           '<div class="box">
    <div class="box-title hidden-xs">
        <h5><a href="#">{{title}}</a></h5>
    </div>
    <a class="visible-xs-block" id="giving-fc-collapse" data-toggle="collapse" href="#{{WidgetId}}-section" aria-expanded="true" aria-controls="{{WidgetId}}-section">
        <div class="box-title">
            <h5>
                <i class="fa fa-chevron-circle-right"></i>&nbsp;&nbsp;{{title}}
            </h5>
        </div>
    </a>
    <div class="collapse in" id="{{WidgetId}}-section">
        <div class="box-content center">
            <h4 class="text-center">{{title}}</h4>
            <div class="chart">
            </div>
        </div>
    </div>
</div>
<script type="text/javascript">
    var {{WidgetId}} = function() {
        var data = new google.visualization.DataTable();
        data.addColumn(''string'', ''Sunday'');
        {{{addcolumns}}}
        data.addRows({{{rowdata}}});
        
        var options = {
            hAxis: {
            title: ''Dates''
            },
            colors: [''blue'', ''red'', ''green'', ''yellow'', ''pink'', ''cyan''],
            hAxis: {
                slantedText: true,
                slantedTextAngle: 30,
                textStyle: {
                    fontSize: 8
                },
                viewWindow: {
                    min: 1,
                    max: {{{interval}}}
                }
            },
            legend: {
                position: "top",
                textStyle: {fontSize: 8},
                alignment: ''center''
            }
        };
        
        var chart = new google.visualization.LineChart(document.querySelector(''#{{WidgetId}}-section .chart''));
        chart.draw(data, options);
    }
    // load and register the chart
    google.charts.load("current", {packages:["corechart"]});
    google.charts.setOnLoadCallback({{WidgetId}});
    WidgetCharts.{{WidgetId}} = {{WidgetId}};
</script>',
           GETDATE(),1,0,0,0,'admin')
           
INSERT INTO [dbo].[ContentKeyWords]
           ([Id],[Word])
     VALUES
           ((select SCOPE_IDENTITY())
           ,'widget')

INSERT INTO [dbo].[Content]
           ([Name],[Title],[Body],[DateCreated],[TypeID],[ThumbID],[RoleID],[OwnerID],[CreatedBy])
     VALUES
           ('WidgetRecentAttendanceTrendsPython','Edit Python Script',
           'divisions = [
    [''Young Couples'', 210],
    [''Young Marrieds'', 211],
    [''Adults 1'', 213]
]
begindate = ''11/15/2019''
beginhour = 1
endhour = 12
Title = ''Recent Attendance Trends''
Interval = ''14''

def GetData(divisions, begindate, beginhour, endhour):
    sql = model.Content(''WidgetRecentAttendanceTrendsSQL'')
    divids = [row[1] for row in divisions]
    sql = sql.replace(''@begindate'', begindate)
    sql = sql.replace(''@beginhour'', str(beginhour))
    sql = sql.replace(''@endhour'', str(endhour))
    sql = sql.replace(''@divs'', str(divids)[1:-1])
    rowcountsql = ''isnull((select sum(HeadCount) from data d where d.DivisionId = {0} and d.ss = dd.ss group by d.ss), 0) d{0}''
    rlist = [rowcountsql.format(d) for d in divids]
    s = '',\n''.join(rlist)
    sql = sql.replace(''@rowcounts'', s)
    return q.QuerySqlJsonArray(sql)

def GetJavascript(divisions, data, Title):
    js = model.Content(''WidgetRecentAttendanceTrendsHTML'')
    addcolumn = "data.addColumn(''number'', ''{}'');"
    alist = [addcolumn.format(s[0]) for s in divisions]
    s = ''\n''.join(alist)
    js = js.replace(''{addcolumns}'', s)
    js = js.replace(''{rowdata}'', data)
    return js

def Get():
    sql = Data.SQLContent
    template = Data.HTMLContent
    Data.results = GetData(divisions, begindate, beginhour, endhour)
    Data.title = Title
    Data.interval = Interval
    data = GetData(divisions, begindate, beginhour, endhour)
    addcolumn = "data.addColumn(''number'', ''{}'');"
    alist = [addcolumn.format(s[0]) for s in divisions]
    Data.addcolumns = ''\n''.join(alist)
    Data.rowdata = data
    print model.RenderTemplate(template)
    
Get()',
           GETDATE(),5,0,0,0,'admin')
          
INSERT INTO [dbo].[ContentKeyWords]
           ([Id],[Word])
     VALUES
           ((select SCOPE_IDENTITY()),'widget')
INSERT INTO [dbo].[Content]
           ([Name],[Title],[Body],[DateCreated],[TypeID],[ThumbID],[RoleID],[OwnerID],[CreatedBy])
     VALUES
           ('WidgetRecentAttendanceTrendsSQL','Edit Sql Script',
           'select HeadCount, 
    o.DivisionId, 
    dbo.SundayForDate(m.MeetingDate) ss, 
    datediff(hour, dbo.SundayForDate(m.MeetingDate), m.MeetingDate) hh
    into #meetingdata
from dbo.Meetings m
join dbo.Organizations o on o.OrganizationId = m.OrganizationId
join dbo.Division d on d.Id = o.DivisionId
where o.DivisionId in (@divs)
and HeadCount > 0

;with data as (
	select HeadCount, DivisionId, ss, hh 
	from #meetingdata
	where ss >= ''@begindate'' and hh > @beginhour AND hh <= @endhour
)
select
	CONVERT(varchar, ss, 111) [Sunday],
	@rowcounts
from data dd
group by dd.ss

drop table #meetingdata',
           GETDATE(),4,0,0,0,'admin')
           
INSERT INTO [dbo].[ContentKeyWords]
           ([Id]
           ,[Word])
     VALUES
           ((select SCOPE_IDENTITY())
           ,'widget')           
END
GO

IF (select count(*) from DashboardWidgets where [Name] like 'Recent Attendance Trends' and [System] = 1) = 0
BEGIN
INSERT INTO [dbo].[DashboardWidgets]
           ([Name]
           ,[Description]
           ,[HTMLContentId]
           ,[PythonContentId]
           ,[SQLContentId]
           ,[Enabled]
           ,[Order]
           ,[System]
           ,[CachePolicy]
           ,[CacheHours])
     VALUES
           ('Recent Attendance Trends'
           ,'Shows a line chart of recent attendance by division'
           ,(select max(Id) from Content where [Name] like 'WidgetRecentAttendanceTrendsHTML')
           ,(select max(Id) from Content where [Name] like 'WidgetRecentAttendanceTrendsPython')
           ,(select max(Id) from Content where [Name] like 'WidgetRecentAttendanceTrendsSQL')
           ,0
           ,(select isnull(max([Order]), 0)+1 from DashboardWidgets)
           ,1
           ,1
           ,6)

INSERT INTO [dbo].[DashboardWidgetRoles]
           ([WidgetId]
           ,[RoleId])
    SELECT SCOPE_IDENTITY() [WidgetId], RoleId FROM dbo.Roles WHERE RoleName in ('Finance', 'FinanceAdmin', 'Admin')
END
GO
