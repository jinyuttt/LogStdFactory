# LogStdFactory
Serilog使用封装，支持配置
## 说明
封装库，xml配置  
xml使用特定节点，灵活配置日志，可以查看样例  
配置：

<Serilog>
	<!--最小输出级别-->
	<MinimumLevel>Debug</MinimumLevel>
	<!--放置扩展库位置，默认当前目录-->
	<SerilogDir>kk</SerilogDir>
	<!--Sinks扩展配置-->
	<SerilogSinks>
	<!--配置各种Sinks节点，Ref-DLL配置所在程序集-->
	<Console Ref-Dll="Serilog.Sinks.Console">
		<![CDATA[配置Console中参数，匹配最合适的配置方法]]>
		<restrictedToMinimumLevel>Debug</restrictedToMinimumLevel>
		<outputTemplate>[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {DateTimeNow} {version:lj}  {NewLine}{Exception} </outputTemplate>
	</Console>
	<File Ref-Dll="Serilog.Sinks.File">
		<path>logs</path>
		<rollingInterval>Day</rollingInterval>
	</File>
	</SerilogSinks>
</Serilog>
