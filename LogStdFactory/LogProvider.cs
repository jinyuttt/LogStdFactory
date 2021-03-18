using Serilog;
using Serilog.Configuration;
using Serilog.Core;
using Serilog.Events;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;
using System.Text.Json;
using System.Threading.Channels;
using System.Threading;

namespace LogStdFactory
{
    public delegate void LogEventCall(LogValue log);
    public class LogProvider
    {
        private static readonly Lazy<LogProvider> logProvider = new Lazy<LogProvider>();

        /// <summary>
        /// 库位置
        /// </summary>
         private string dirskin = "";
    
        public static LogProvider Instance
        {
            get { return logProvider.Value; }
        }


        public event LogEventCall OnMsgCall;

       

        private static string cfgPath = "config/LogCfg.xml";

        public LogProvider()
        {
            Init();
            Start();
        }

        /// <summary>
        /// 配置文件
        /// </summary>
        /// <param name="file">文件路径</param>
        public static void SinkConfiguration(string file)
        {
            if(!string.IsNullOrEmpty(file))
            {
                FileInfo info = new FileInfo(file);
                if(info.Exists)
                {
                    cfgPath = file;
                }
            }
        }

        /// <summary>
        /// 开始分发
        /// </summary>
        private void Start()
        {
            Thread thread = new Thread(async () =>
             {
                 while (await SeqEnricher.LogChannel.Reader.WaitToReadAsync())
                 {
                     if (SeqEnricher.LogChannel.Reader.TryRead(out var message))
                     {
                         string msg = message.RenderMessage();
                         var logmsg = new LogValue()
                         { Message = msg , MessageProperty=new Dictionary<string, string>()};
                         var p = message.Properties.ToList();
                         foreach (var kv in p)
                         {
                             logmsg.MessageProperty[kv.Key] = kv.Value.ToString();
                         }
                         if (OnMsgCall!=null)
                         {
                             OnMsgCall(logmsg);
                         }
                     }
                 }
             });
            thread.IsBackground = true;
            thread.Start();
        }

        /// <summary>
        /// 初始化
        /// </summary>
        public void Init()
        {


            XmlDocument document = new XmlDocument();
            document.Load(cfgPath);
          
           var cfg= document.GetElementsByTagName("Serilog");//找到Serilog配置节点
            var lcfg = new LoggerConfiguration()
                 .Enrich.With(new DateTimeNowEnricher())
                 .Enrich.With(new SeqEnricher())
                 .Enrich.WithProperty("version", "1.0.0")
                 .Enrich.WithThreadId()
                 .Enrich.FromLogContext();

            string MinimumLevel = "";
            //
           
           
            if (cfg.Count>0)
            {
                //取出最后一个
                var serilogCfg =(XmlElement) cfg[cfg.Count - 1];
                var dirs =serilogCfg.GetElementsByTagName("SerilogDir");//取出配置目录
                if (dirs.Count > 0)
                {
                    dirskin = dirs.Item(dirs.Count - 1).InnerText;
                }
                else
                {
                    dirskin = AppDomain.CurrentDomain.BaseDirectory;
                }
                var minis = serilogCfg.GetElementsByTagName("MinimumLevel");//读取最小级别
                if (minis.Count > 0)
                {
                    MinimumLevel = minis.Item(minis.Count - 1).InnerText;
                }
                var sinksCfg = serilogCfg.GetElementsByTagName("SerilogSinks");//读取Sink配置

                foreach (XmlNode node in sinksCfg)
                {
                    //
                    
                    foreach(XmlNode child in node.ChildNodes)
                    {
                        if(child.NodeType!= XmlNodeType.Element)
                        {
                            continue;
                        }
                        var mth = child.Name.Trim();
                        var dll = "";
                       
                        foreach(XmlAttribute sll in child.Attributes)
                        {
                            if(sll.Name.ToLower().Trim()== "ref-dll")
                            {
                                dll = sll.Value;
                            }
                        }
                        Dictionary<string, string> dic = new Dictionary<string, string>();
                        foreach (XmlNode p in child.ChildNodes)
                        {
                            if (p.NodeType != XmlNodeType.Element)
                            {
                                continue;
                            }
                            dic[p.Name.ToLower()] = p.InnerText;
                        }
                        //
                        Load(lcfg.WriteTo, dll, mth, dic);
                    }
                    //
                   
                }
            }
            MinimumLevel = MinimumLevel.ToLower();
            if (!string.IsNullOrEmpty(MinimumLevel))
            {
                switch (MinimumLevel)
                {
                    case "debug":
                        lcfg = lcfg.MinimumLevel.Debug();
                        break;
                    case "error":
                        lcfg = lcfg.MinimumLevel.Error();
                        break;
                    case "fatal":
                        lcfg = lcfg.MinimumLevel.Fatal();
                        break;
                    case "info":
                        lcfg = lcfg.MinimumLevel.Information();
                        break;
                    case "verbose":
                        lcfg = lcfg.MinimumLevel.Verbose();
                        break;
                    case "warn":
                        lcfg = lcfg.MinimumLevel.Warning();
                        break;

                }

            }
            Log.Logger = lcfg.CreateLogger();
        }
      
        /// <summary>
        /// 读取dll
        /// </summary>
        /// <returns></returns>
        private string[] GetFiles()
        {
            DirectoryInfo info = new DirectoryInfo(dirskin);
            if(!info.Exists)
            {
                return new string[0];
            }
            string[] files = Directory.GetFiles(info.FullName, "Serilog.Sinks.*");
            return files;

        }

        /// <summary>
        /// 加载配置方法
        /// </summary>
        /// <param name="loggerSink"></param>
        /// <param name="dll"></param>
        /// <param name="mth"></param>
        /// <param name="dic"></param>
        private void Load(LoggerSinkConfiguration  loggerSink, string dll,string mth,Dictionary<string,string> dic)
        {
            List<MethodInfo> finds = new List<MethodInfo>();
            if (string.IsNullOrEmpty(dll))
            {
                //没有配置对应的DLL时，从目录中查找所有的DLL
                var files = GetFiles();
                foreach(var file in files)
                {
                    var cur = Assembly.LoadFile(file);
                    var mthcur = GetExtensionMethods(cur, typeof(LoggerSinkConfiguration));
                    finds= mthcur.Where(X => X.Name == mth).ToList();
                    if(finds.Count>0)
                    {
                        //找到了
                        break;
                    }
                }
            }
            else
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, dll + ".dll");
                var asm = Assembly.LoadFile(path);
                finds = GetExtensionMethods(asm, typeof(LoggerSinkConfiguration)).ToList();
            }
            foreach(var cur in finds)
            {
                var parameters = cur.GetParameters();
                if(dic.Keys.Count(X => parameters.Count(Y => Y.Name.ToLower() == X)==0)>0)
                {
                    continue;//配置的名称方法中没有

                }

                object[] args = new object[parameters.Length];
                args[0] = loggerSink;
                for (int i = 1; i < args.Length; i++)
                {
                  
                   
                    if (dic.ContainsKey(parameters[i].Name.ToLower()))
                    {
                        //
                        string str = dic[parameters[i].Name.ToLower()];
                        if (parameters[i].ParameterType.IsEnum)
                        {
                            object v = null;
                            if (Enum.TryParse(parameters[i].ParameterType, str, true, out v))
                            {
                                args[i] = v;
                            }
                            else
                            {
                                break;
                            }
                        }
                        else if (parameters[i].ParameterType == typeof(string))
                        {
                            args[i] = str;
                        }
                        else if (parameters[i].ParameterType.IsInterface || parameters[i].ParameterType.IsClass)
                        {
                            //json
                            try
                            {
                               args[i] = JsonSerializer.Deserialize(str, parameters[i].ParameterType);
                            }
                            catch(Exception ex)
                            {
                                args[i] = null;
                               Console.WriteLine("日志加载异常" + ex.Message);
                            }
                        }
                    }
                    else if (parameters[i].HasDefaultValue)
                    {
                        args[i] = parameters[i].DefaultValue;
                    }
                    else if (!parameters[i].ParameterType.IsInterface && !parameters[i].ParameterType.IsClass)
                    {
                        break;
                    }
                    else
                    {
                        args[i] = null;
                    }
                }
                try
                {
                    cur.Invoke(cur.IsStatic ? null : this, args);
                    break;//成功
                }
                catch(Exception ex)
                {
                    Console.WriteLine("日志加载异常"+ex.Message);
                }
            }

        }

        /// <summary>
        /// 获取扩展方法
        /// </summary>
        /// <param name="assembly"></param>
        /// <param name="extendedType"></param>
        /// <returns></returns>
        static IEnumerable<MethodInfo> GetExtensionMethods(Assembly assembly, Type extendedType)
        {
            var query = from type in assembly.GetTypes()
                        where !type.IsGenericType && !type.IsNested
                        from method in type.GetMethods(BindingFlags.Static
                            | BindingFlags.Public | BindingFlags.NonPublic)
                        where method.IsDefined(typeof(System.Runtime.CompilerServices.ExtensionAttribute), false)
                        where method.GetParameters()[0].ParameterType == extendedType
                        select method;
            return query;
        }
       
        public void Debug(string msg)
        {
            Log.Debug(msg);
        }
       
        public void Debug<T>(T v,string msg)
        {
            Log.Debug<T>(msg,v);
        }
        public void Info(string msg)
        {
            Log.Information(msg);
        }
        public void Info<T>(string msg,T v)
        {
            Log.Information(msg,v);
        }
        public void Error(string msg)
        {
            Log.Error(msg);
        }
        public void Error(Exception  ex,string msg)
        {
            Log.Error(ex,msg);
        }
        public void Fatal(string msg)
        {
            Log.Fatal(msg);
        }

        public void Close()
        {
            SeqEnricher.LogChannel.Writer.Complete();
            Log.CloseAndFlush();
        }
    }


    public class LogValue
    {
        public string Message { get; set; }

        public Dictionary<string,string> MessageProperty { get; set; }
    }

    #region Serilog 相关设置
    class DateTimeNowEnricher : ILogEventEnricher
    {
        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(
                "DateTimeNow", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")));
        }
    }

    class SeqEnricher : ILogEventEnricher
    {
       static long id = 0;
      public  static readonly Channel<LogEvent> LogChannel = Channel.CreateBounded<LogEvent>(new BoundedChannelOptions(1000)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        });
        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
           
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(
                "MsgID", System.Threading.Interlocked.Increment(ref id)));
            LogChannel.Writer.WriteAsync(logEvent);
        }
    }
    #endregion
}
