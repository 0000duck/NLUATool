//***************************************************************************
// 
//    Copyright (c) Microsoft Corporation. All rights reserved.
//    This code is licensed under the Visual Studio SDK license terms.
//    THIS CODE IS PROVIDED *AS IS* WITHOUT WARRANTY OF
//    ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING ANY
//    IMPLIED WARRANTIES OF FITNESS FOR A PARTICULAR
//    PURPOSE, MERCHANTABILITY, OR NON-INFRINGEMENT.
//
//***************************************************************************

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.Language.Intellisense;
using System.Collections.ObjectModel;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Utilities;
using System.Reflection;
using LanguageService.Formatting.Options;
using System.Text.RegularExpressions;
using System.IO;

namespace OokLanguage
{
    [Export(typeof(ICompletionSourceProvider))]
    [ContentType("luacode!")]
    [Name("ookCompletion")]
    class OokCompletionSourceProvider : ICompletionSourceProvider
    {
        public ICompletionSource TryCreateCompletionSource(ITextBuffer textBuffer)
        {
            return new OokCompletionSource(textBuffer);
        }
    }

    public class MemsInfo
    {
        public MemsInfo(string name,string type,string info)
        {
            this.Name = name;
            this.type = type;
            this.info = info;
        }
        public string Name { get; set; }
        public string info { get; set; }
        public int count { get; set; }
        public string type { get; set; }

      



    }

    class OokCompletionSource : ICompletionSource
    {
        private ITextBuffer _buffer;
        private bool _disposed = false;

        private  List<string> LocalDir = new List<string>();

        private  Dictionary<string, Type> TypeDir = new Dictionary<string, Type>();
        private  Dictionary<string, Assembly> AssDir = new Dictionary<string, Assembly>();

        private  Dictionary<Type, List<MemsInfo>> typeMembers = new Dictionary<Type, List<MemsInfo>>();
        private  Dictionary<Type, List<MemsInfo>> typeMethods = new Dictionary<Type, List<MemsInfo>>();

        private System.Threading.Tasks.Task task;

        SourceTextCache txtCache = new SourceTextCache();
        public  Assembly GetAssembly(string namespaces)
        {

            if (namespaces.IndexOf("System.IO") >= 0)
                return typeof(System.IO.File).Assembly;
            else if (namespaces.IndexOf("System.Net.Socket") >= 0)
                return typeof(System.Net.Sockets.Socket).Assembly;
            else if (namespaces.IndexOf("System.Net") >= 0)
                return typeof(System.Net.WebClient).Assembly;
            else if (namespaces.IndexOf("System.Net.Http") >= 0)
                return typeof(System.Net.Http.HttpClient).Assembly;
            else if (namespaces.IndexOf("System.Net.Http.WebRequest") >= 0)
                return typeof(System.Net.Http.WebRequestHandler).Assembly;
            else if (namespaces.IndexOf("System.Net.Drawing") >= 0)
                return typeof(System.Drawing.Graphics).Assembly;
            else if (namespaces.IndexOf("System.Windows.Forms") >= 0)
                return typeof(System.Windows.Forms.MessageBox).Assembly;
            else if (namespaces.IndexOf("System.Xaml") >= 0)
                return typeof(System.Xaml.XamlReader).Assembly;
            else if (namespaces.IndexOf("System.Xml.Linq") >= 0)
                return typeof(System.Xml.Linq.XDocument).Assembly;
            else if (namespaces.IndexOf("System.Xml.Serialization") >= 0)
                return typeof(System.Xml.Serialization.XmlAttributes).Assembly;
            else if (namespaces.IndexOf("System.Xml") >= 0)
                return typeof(System.Xml.XmlElement).Assembly;
            else if (namespaces.IndexOf("System.Configuration") >= 0)
                return typeof(System.Configuration.Configuration).Assembly;
            else if (namespaces.IndexOf("System.Configuration.Install") >= 0)
                return typeof(System.Configuration.Install.Installer).Assembly;
            else if (namespaces.IndexOf("System.Configuration.Install") >= 0)
                return typeof(System.Configuration.Install.Installer).Assembly;
            else if (namespaces.IndexOf("System.Data.SqlClient") >= 0)
                return typeof(System.Data.SqlClient.SqlConnection).Assembly;
            else if (namespaces.IndexOf("System.Data.OracleClient") >= 0)
                return typeof(System.Data.OracleClient.OracleLob).Assembly;
            else if (namespaces.IndexOf("System.Data.Odbc") >= 0)
                return typeof(System.Data.Odbc.OdbcConnection).Assembly;
            else if (namespaces.IndexOf("System.Data.Linq") >= 0)
                return typeof(System.Data.Linq.SqlClient.SqlHelpers).Assembly;
            else if(namespaces.IndexOf("System.Data") >= 0)
                return typeof(System.Data.DataSet).Assembly;
            else if (namespaces.IndexOf("System") >= 0)
                return typeof(IntPtr).Assembly;
            else
            {
                if(AssDir.ContainsKey(namespaces))
                {
                    return AssDir[namespaces];
                }else
                {
                    return typeof(IntPtr).Assembly;
                }

            }

        }


        public void LoadingNameSpace(string namespaces)
        {

            namespaces = namespaces.Trim();
            namespaces = namespaces.Trim('"');



            if (File.Exists(namespaces))
            {
                var ass = Assembly.LoadFile(namespaces);

                foreach (var item in ass.GetTypes())
                {
                    if (!AssDir.ContainsKey(item.Namespace))
                    {
                        AssDir.Add(item.Namespace, ass);
                    }
                }

                return;
            }


            if (string.IsNullOrEmpty(namespaces))
                return;

            var types = GetAssembly(namespaces);

            if (types == null)
            {
                types = typeof(String).Assembly;
                LoadingNameSpace(types, namespaces);
            }
            else
            {
                LoadingNameSpace(types, namespaces);
            }

        }

        public  void LoadingNameSpace(Assembly assembly,string namespaces)
        {
            Type[] types= assembly.GetTypes();

            foreach (var item in types)
            {
                if(item.IsPublic)
                {  
                    if (item.Namespace== namespaces)
                    {
                        string[] name = item.Name.Split(new char[] { '`' }, StringSplitOptions.RemoveEmptyEntries);
                        if (name.Length == 2)
                        {
                            if (!TypeDir.ContainsKey(name[0]))
                                TypeDir.Add(name[0], item);
                        }
                        else
                        {
                            if (!TypeDir.ContainsKey(item.Name))
                                TypeDir.Add(item.Name, item);
                        }
                    }
                }
            }
        }


        private void GetTypeMethods(List<MemsInfo> tmp, Type type, bool isstatic)
        {


            if (type.IsClass)
            {
                if (isstatic)
                {
                    foreach (var item in (from p in type.GetMethods()
                                          where p.IsStatic && p.IsPublic
                                          select p))
                    {

                        if (item.Name.IndexOf("get_") == 0 || item.Name.IndexOf("set_") == 0)
                        {
                            continue;
                        }
                        else
                        {
                            MemsInfo method = new MemsInfo(item.Name, item.ToString(), "Is Method");

                            if (tmp.Find(p => p.type == method.type) == null)
                                tmp.Add(method);

                        }

                    }
                }
                else
                {
                    foreach (var item in (from p in type.GetMethods()
                                          where p.IsStatic == false && p.IsPublic
                                          select p))
                    {

                        if (item.Name.IndexOf("get_") == 0 || item.Name.IndexOf("set_") == 0)
                        {
                            continue;
                        }
                        else
                        {
                            MemsInfo method = new MemsInfo(item.Name, item.ToString(), "Is Method");

                            if (tmp.Find(p => p.type == method.type) == null)
                                tmp.Add(method);
                        }

                    }
                }

                if (type.BaseType != null)
                    GetTypeMethods(tmp, type.BaseType, isstatic);

            }


        }

        private  List<MemsInfo> GetTypeMethods(Type type,bool isstatic)
        {
            if (!typeMethods.ContainsKey(type))
            {
                List<MemsInfo> tmp = new List<MemsInfo>();
             
                if (type.IsClass)
                {
                    if (isstatic)
                    {
                        foreach (var item in (from p in type.GetMethods()
                                              where p.IsStatic && p.IsPublic
                                              select p))
                        {

                            if (item.Name.IndexOf("get_") == 0 || item.Name.IndexOf("set_") == 0)
                            {
                                continue;
                            }
                            else
                            {
                                MemsInfo method = new MemsInfo(item.Name,item.ToString(),"Is Method");

                                if (tmp.Find(p => p.type == method.type) == null)
                                    tmp.Add(method);

                            }

                        }
                    }else
                    {
                        foreach (var item in (from p in type.GetMethods()
                                              where p.IsStatic==false && p.IsPublic
                                              select p))
                        {

                            if (item.Name.IndexOf("get_") == 0 || item.Name.IndexOf("set_") == 0)
                            {
                                continue;
                            }
                            else
                            {
                                MemsInfo method = new MemsInfo(item.Name, item.ToString(), "Is Method");

                                if (tmp.Find(p => p.type == method.type) == null)
                                    tmp.Add(method);
                            }

                        }
                    }

                }

                tmp.Sort((a, b) => a.Name.CompareTo(b.Name));

                typeMethods[type] = tmp;
                return tmp;
            }
            else
            {
                return typeMethods[type];
            }
        }



        private  void GetTypeMembers(List<MemsInfo> tmp,Type type, bool isStatic)
        {

           
            if (type.IsEnum)
            {
                foreach (var item in type.GetEnumNames())
                {
                    tmp.Add(new MemsInfo(item, type.Name + "." + item, "Is Enum"));
                }

            }
            else if (type.IsClass)
            {

                foreach (var item in (from p in type.GetMethods()
                                      where p.IsStatic && p.IsPublic
                                      select p))
                {

                    if (item.Name.IndexOf("get_") == 0 || item.Name.IndexOf("set_") == 0)
                    {
                        continue;
                    }
                    else
                    {
                        MemsInfo x = null;
                        if ((x = tmp.Find(p => p.Name == item.Name)) != null)
                        {
                            x.type += "\r\n" + item.ToString();
                        }
                        else
                        {
                            MemsInfo method = new MemsInfo(item.Name, item.ToString(), "Is Method");
                            tmp.Add(method);

                        }

                    }

                }


                foreach (var item in (from p in type.GetProperties()
                                      select p))
                {

                    if (tmp.Find(p => p.Name == item.Name) == null)
                        tmp.Add(new MemsInfo(item.Name, item.ToString(), "Is Properties"));

                }

                if (isStatic)
                {
                    foreach (var item in (from p in type.GetFields()
                                          where p.IsStatic && p.IsPublic
                                          select p))
                    {

                        if (tmp.Find(p => p.Name == item.Name) == null)
                            tmp.Add(new MemsInfo(item.Name, item.ToString(), "Is Field"));
                    }
                }
                else
                {
                    foreach (var item in (from p in type.GetFields()
                                          where p.IsStatic == false && p.IsPublic
                                          select p))
                    {

                        if (tmp.Find(p => p.Name == item.Name) == null)
                            tmp.Add(new MemsInfo(item.Name, item.ToString(), "Is Field"));

                    }
                }

                if (type.BaseType != null)
                    GetTypeMembers(tmp, type.BaseType, isStatic);
            }

          

        }


        private  List<MemsInfo> GetTypeMembers(Type type,bool isStatic)
        {
            if (!typeMembers.ContainsKey(type))
            {
                List<MemsInfo> tmp = new List<MemsInfo>();

                if (type.IsEnum)
                {
                    foreach (var item in type.GetEnumNames())
                    {
                        tmp.Add(new MemsInfo(item,type.Name+"."+item, "Is Enum"));
                    }                   

                }
                else if (type.IsClass)
                {

                    foreach (var item in (from p in type.GetMethods()
                                          where p.IsStatic && p.IsPublic
                                          select p))
                    {

                        if (item.Name.IndexOf("get_") == 0 || item.Name.IndexOf("set_") == 0)
                        {
                            continue;
                        }
                        else
                        {

                            MemsInfo x = null;
                            if ((x=tmp.Find(p => p.Name == item.Name)) != null)
                            {
                                x.type += "\r\n" + item.ToString();
                            }
                            else
                            {
                                MemsInfo method = new MemsInfo(item.Name, item.ToString(), "Is Method");
                                tmp.Add(method);

                            }
                        }

                    }


                    foreach (var item in (from p in type.GetProperties()
                                          select p))
                    {
                                               
                        if (tmp.Find(p => p.Name == item.Name) == null)
                            tmp.Add(new MemsInfo(item.Name,item.ToString(), "Is Properties"));

                    }

                    if (isStatic)
                    {
                        foreach (var item in (from p in type.GetFields()
                                              where p.IsStatic && p.IsPublic
                                              select p))
                        {                                                     

                            if (tmp.Find(p => p.Name == item.Name) == null)
                                tmp.Add(new MemsInfo(item.Name, item.ToString(), "Is Field"));
                        }
                    }
                    else
                    {
                        foreach (var item in (from p in type.GetFields()
                                              where p.IsStatic==false && p.IsPublic
                                              select p))
                        {

                            if (tmp.Find(p => p.Name == item.Name) == null)
                                tmp.Add(new MemsInfo(item.Name, item.ToString(),"Is Field"));

                        }
                    }

                    if (type.BaseType != null)
                        GetTypeMembers(tmp, type.BaseType, isStatic);
                }

                tmp.Sort((a, b) => a.Name.CompareTo(b.Name));

                typeMembers[type] = tmp;
                return tmp;
            }
            else
            {
                return typeMembers[type];
            }
        }



        public OokCompletionSource(ITextBuffer buffer)
        {
            _buffer = buffer;


            LocalDir.Add("import");
            LocalDir.Add("function");
            LocalDir.Add("require");
            LocalDir.Add("repeat");
            LocalDir.Add("break");
            LocalDir.Add("and");
            LocalDir.Add("else");
            LocalDir.Add("false");
            LocalDir.Add("true");
            LocalDir.Add("return");
            LocalDir.Add("while");
            LocalDir.Add("local");
            LocalDir.Add("elseif");
            LocalDir.Add("until");
            LocalDir.Add("then");
            LocalDir.Add("print");
            LocalDir.Add("nil");
            LocalDir.Add("not");
            LocalDir.Add("end");
            LocalDir.Add("CLRPackage");
            LocalDir.Add("pcall");
            LocalDir.Add("math");
            LocalDir.Add("assert");
            LocalDir.Add("collectgarbage");
            LocalDir.Add("date");
            LocalDir.Add("error");
            LocalDir.Add("gcinfo");
            LocalDir.Add("getfenv");
            LocalDir.Add("getmetatable");
            LocalDir.Add("loadstring");
            LocalDir.Add("next");
            LocalDir.Add("select");
            LocalDir.Add("setfenv");
            LocalDir.Add("setmetatable");
            LocalDir.Add("time");
            LocalDir.Add("type");
            LocalDir.Add("unpack");
            LocalDir.Add("xpcall");
            LocalDir.Add("format");
            LocalDir.Add("gsub");
            LocalDir.Add("strbyte");
            LocalDir.Add("strchar");
            LocalDir.Add("strchar");
            LocalDir.Add("strfind");
            LocalDir.Add("strlen");
            LocalDir.Add("strlower");
            LocalDir.Add("strmatch");
            LocalDir.Add("strrep");
            LocalDir.Add("strsub");
            LocalDir.Add("strupper");
            LocalDir.Add("tonumber");
            LocalDir.Add("tostring");
            LocalDir.Add("ipairs");
            LocalDir.Add("pairs");

            // LoadingNameSpace(typeof(string).Assembly, "System");

            _buffer.Changed += _buffer_Changed;
           
             LoadingNameSpace();


        }

       

        private void _buffer_Changed(object sender, TextContentChangedEventArgs e)
        {
            //if (e.EditTag == null)
            //{

            if (e.Changes.Count > 0)
            {
                foreach (var item in e.Changes)
                {
                    if (string.IsNullOrEmpty(item.NewText) && !string.IsNullOrEmpty(item.OldText))
                    {
                        if (item.OldText.IndexOf("import") >= 0|| item.OldText=="t"|| item.OldText=="i")
                        {
                            TypeDir.Clear();
                            typeMembers.Clear();
                            typeMethods.Clear();
                        }
                    }
                    else if (!string.IsNullOrEmpty(item.NewText) && string.IsNullOrEmpty(item.OldText))
                    {
                        LoadingNameSpace();

                    }

                }

            }

            //}
            //else
            //{
            //    LoadingNameSpace();
            //}
        }

        private void LoadingNameSpace()
        {

            if (task == null)
            {
                task = new System.Threading.Tasks.Task(() =>
                  {

                    
                      foreach (var line in txtCache.Get(_buffer.CurrentSnapshot).Lines)
                      {
                          string txt = line.Text;

                          if (txt.IndexOf("import") >= 0)
                          {
                              var rx = Regex.Matches(txt, "(?<=import[ ,\t]).+");

                              foreach (Match item in rx)
                              {
                                  if (item.Success)
                                  {
                                      string val = item.Value.Trim(' ', '\t', '"');
                                      LoadingNameSpace(val);

                                  }
                              }
                          }
                      }

                      task = null;
                  });

                task.Start();

            }
        }

        public void AugmentCompletionSession(ICompletionSession session, IList<CompletionSet> completionSets)
        {
            if (_disposed)
                throw new ObjectDisposedException("OokCompletionSource");


            ITextSnapshot snapshot = _buffer.CurrentSnapshot;

            
            var triggerPoint = (SnapshotPoint)session.GetTriggerPoint(snapshot);

            if (triggerPoint == null)
                return;

            var line = triggerPoint.GetContainingLine();


            SnapshotPoint start = triggerPoint;

            while (start > line.Start && !char.IsWhiteSpace((start - 1).GetChar()))
            {
                start -= 1;
            }

            
            char[] indexchar = new char[] { ' ', '\t', ':', '(', '[', ';', '>', '=', '.',':' };


            List<Completion> completions = new List<Completion>();

            string linetxt = line.GetText();

          

            if (linetxt.Length > 0 && linetxt[linetxt.Length - 1] == '.')
            {
                string currentTxt = linetxt;
                bool isStatic = true;
                byte chueck = 0;
                tp:
                string lstr = currentTxt.Substring(0, currentTxt.Length - 1);
                int lastindex = lstr.LastIndexOfAny(indexchar);
                lastindex++;
                var cmd = lstr.Substring(lastindex, lstr.Length - lastindex);

                if (TypeDir.ContainsKey(cmd))
                {

                    foreach (var member in GetTypeMembers(TypeDir[cmd],isStatic))
                    {
                        completions.Add(new Completion(member.Name,member.Name, member.type + "\r\n"+member.info,null,""));
                    }
                }
                else
                {
                    currentTxt = linetxt.Substring(0, linetxt.LastIndexOf(cmd));
                    if (currentTxt.Length > 0)
                    {
                        chueck++;
                        isStatic = false;

                        if(chueck<50)
                            goto tp;
                    }
                }


                if (completions.Count > 0)
                {

                    start = line.End;
                    triggerPoint = line.End;

                    var applicableTo = snapshot.CreateTrackingSpan(new SnapshotSpan(start, triggerPoint), SpanTrackingMode.EdgeInclusive);

                    completionSets.Add(new CompletionSet("All", "All", applicableTo, completions, Enumerable.Empty<Completion>()));
                }
            }
            else if (linetxt.Length > 0 && linetxt[linetxt.Length - 1] == ':')
            {

                string currentTxt = linetxt;
                bool isStatic = true;
                byte chueck = 0;
                
                tp:

                string lstr = currentTxt.Substring(0, currentTxt.Length - 1);
                int lastindex = lstr.LastIndexOfAny(indexchar);
                lastindex++;
                var cmd = lstr.Substring(lastindex, lstr.Length - lastindex);

                if (TypeDir.ContainsKey(cmd))
                {

                    foreach (var member in GetTypeMethods(TypeDir[cmd], isStatic))
                    {
                        completions.Add(new Completion(member.Name, member.Name, member.type + "\r\n" + member.info, null, ""));
                    }
                }
                else
                {
                    currentTxt = linetxt.Substring(0, linetxt.LastIndexOf(cmd));
                    if (currentTxt.Length > 0)
                    {
                        chueck++;
                        isStatic = false;

                        if (chueck < 50)
                            goto tp;
                    }
                }

                if (completions.Count > 0)
                {

                    start = line.End;
                    triggerPoint = line.End;

                    var applicableTo = snapshot.CreateTrackingSpan(new SnapshotSpan(start, triggerPoint), SpanTrackingMode.EdgeInclusive);

                    completionSets.Add(new CompletionSet("All", "All", applicableTo, completions, Enumerable.Empty<Completion>()));
                }
            }           
            else
            {
                string end = line.GetText().ToLower();
                int lastindex = end.LastIndexOfAny(indexchar);
                lastindex++;
                string cmd = end.Substring(lastindex, end.Length - lastindex);
                if (cmd.Length > 1)
                {
                    var list = LocalDir.FindAll(p => p.ToLower().IndexOf(cmd) == 0);

                    if (list.Count > 0)
                    {

                        foreach (var str in list)
                        {
                            completions.Add(new Completion(str));
                        }

                    }

                    var dirlist = TypeDir.Keys.Where(p => p.ToLower().IndexOf(cmd) == 0);

                    if (dirlist.Count() > 0)
                    {
                        foreach (var str in dirlist)
                        {
                            completions.Add(new Completion(str));
                        }

                    }
                }

                if (completions.Count > 0)
                {
                    start = triggerPoint - cmd.Length;

                    var applicableTo = snapshot.CreateTrackingSpan(new SnapshotSpan(start, triggerPoint), SpanTrackingMode.EdgeInclusive);

                    completionSets.Add(new CompletionSet("All", "All", applicableTo, completions, Enumerable.Empty<Completion>()));
                }
               
            }



          
        }

        public void Dispose()
        {
            _buffer.Changed -= _buffer_Changed;
            _disposed = true;
        }


    }
}

