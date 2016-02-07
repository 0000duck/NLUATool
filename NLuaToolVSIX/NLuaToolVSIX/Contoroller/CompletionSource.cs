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
using LanguageService;
using System.Collections.Concurrent;

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

        private ConcurrentDictionary<string, Type> TypeDir = new ConcurrentDictionary<string, Type>();
        private ConcurrentDictionary<string, Assembly> AssDir = new ConcurrentDictionary<string, Assembly>();

        private ConcurrentDictionary<string, Type> defDir = new ConcurrentDictionary<string, Type>();



        private ConcurrentDictionary<Type, List<MemsInfo>> typeMembers = new ConcurrentDictionary<Type, List<MemsInfo>>();
        private ConcurrentDictionary<Type, List<MemsInfo>> typeMethods = new ConcurrentDictionary<Type, List<MemsInfo>>();

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
            namespaces = namespaces.Trim(';');
            namespaces = namespaces.Trim('"');



            if (File.Exists(namespaces))
            {
                var ass = Assembly.LoadFile(namespaces);

                foreach (var item in ass.GetTypes())
                {
                    if (!AssDir.ContainsKey(item.Namespace))
                    {
                        AssDir.TryAdd(item.Namespace, ass);
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
                                TypeDir.TryAdd(name[0], item);
                        }
                        else
                        {
                            if (!TypeDir.ContainsKey(item.Name))
                                TypeDir.TryAdd(item.Name, item);
                        }
                    }
                }
            }
        }


        private void GetTypeMethods(List<MemsInfo> tmp, Type type, bool isstatic)
        {


            if (type.IsClass)
            {

                foreach (var item in (from p in type.GetMethods()
                                      where p.IsStatic == isstatic && p.IsPublic
                                      select p))
                {

                    if (item.Name.IndexOf("get_") == 0 || item.Name.IndexOf("set_") == 0 || item.Name.IndexOf("add_") == 0 || item.Name.IndexOf("remove_") == 0)
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

                    foreach (var item in (from p in type.GetMethods()
                                          where p.IsStatic == isstatic && p.IsPublic
                                          select p))
                    {

                        if (item.Name.IndexOf("get_") == 0 || item.Name.IndexOf("set_") == 0 || item.Name.IndexOf("add_") == 0 || item.Name.IndexOf("remove_") == 0)
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
                if (isStatic)
                {
                    foreach (var item in (from p in type.GetMethods()
                                          where p.IsStatic && p.IsPublic
                                          select p))
                    {

                        if (item.Name.IndexOf("get_") == 0 || item.Name.IndexOf("set_") == 0 || item.Name.IndexOf("add_") == 0 || item.Name.IndexOf("remove_") == 0)
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
                }


                foreach (var item in (from p in type.GetProperties()
                                      select p))
                {

                    if (tmp.Find(p => p.Name == item.Name) == null)
                        tmp.Add(new MemsInfo(item.Name, item.ToString(), "Is Properties"));

                }

                foreach (var item in (from p in type.GetEvents()
                                      select p))
                {

                    if (tmp.Find(p => p.Name == item.Name) == null)
                        tmp.Add(new MemsInfo(item.Name, item.EventHandlerType.ToString() + "\r\n：Add(fuction(...){})+\r\n" + GetGetMethodstr(item.EventHandlerType), "Is Events"));

                }

                foreach (var item in (from p in type.GetFields()
                                      where p.IsStatic == isStatic && p.IsPublic
                                      select p))
                {

                    if (tmp.Find(p => p.Name == item.Name) == null)
                        tmp.Add(new MemsInfo(item.Name, item.ToString(), "Is Field"));
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
                        tmp.Add(new MemsInfo(item, type.Name + "." + item, "Is Enum"));
                    }

                }
                else if (type.IsClass)
                {

                    if (isStatic)
                    {
                        foreach (var item in (from p in type.GetMethods()
                                              where p.IsStatic && p.IsPublic
                                              select p))
                        {

                            if (item.Name.IndexOf("get_") == 0 || item.Name.IndexOf("set_") == 0 || item.Name.IndexOf("add_") == 0 || item.Name.IndexOf("remove_") == 0)
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
                    }

                    foreach (var item in (from p in type.GetProperties()
                                          select p))
                    {

                        if (tmp.Find(p => p.Name == item.Name) == null)
                            tmp.Add(new MemsInfo(item.Name, item.ToString(), "Is Properties"));

                    }

                    foreach (var item in (from p in type.GetEvents()
                                          select p))
                    {

                        if (tmp.Find(p => p.Name == item.Name) == null)
                            tmp.Add(new MemsInfo(item.Name, item.EventHandlerType.ToString()+"\r\n：Add(fuction(...){})+\r\n" + GetGetMethodstr(item.EventHandlerType), "Is Events"));

                    }


                    foreach (var item in (from p in type.GetFields()
                                          where p.IsStatic==isStatic && p.IsPublic
                                          select p))
                    {

                        if (tmp.Find(p => p.Name == item.Name) == null)
                            tmp.Add(new MemsInfo(item.Name, item.ToString(), "Is Field"));
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
            LocalDir.Add("MakeGenericType");
            LocalDir.Add("CallGenricMethod");
            LocalDir.Add("luanet");
            LocalDir.Add("import_type");
            LocalDir.Add("namespace");
            LocalDir.Add("each");
            LocalDir.Add("load_assembly");

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
                            defDir.Clear();
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


        SyntaxKind[] localdefvar = new SyntaxKind[]
        {
              SyntaxKind.LocalKeyword,
              SyntaxKind.Identifier,
              SyntaxKind.AssignmentOperator
        };

        private bool ChecklocalDef(List<Token> token)
        {
            if (token.Count < localdefvar.Length)
                return false;

            bool isDef = true;
            for (int i = 0; i < localdefvar.Length; i++)
            {
                if (token[i].Kind != localdefvar[i])
                {
                    isDef = false;
                    break;
                }
            }

            if (isDef)
            {
                Token Name = token[1];
                Token type = token[3];

                if (TypeDir.ContainsKey(type.Text))
                {
                    defDir[Name.Text] = TypeDir[type.Text];
                }

                return true;
            }

            return false;

        }

        SyntaxKind[] defvar = new SyntaxKind[]
          {
              SyntaxKind.Identifier,
              SyntaxKind.AssignmentOperator             
          };

        private bool CheckDef(List<Token> token)
        {
            if (token.Count < defvar.Length)
                return false;

            bool isDef = true;
            for (int i = 0; i < defvar.Length; i++)
            {
                if (token[i].Kind != defvar[i])
                {
                    isDef = false;
                    break;
                }
            }

            if (isDef)
            {
                Token Name = token[0];
                Token type = token[2];

                if(TypeDir.ContainsKey(type.Text))
                {
                    defDir[Name.Text] = TypeDir[type.Text];
                }

                return true;
            }

            return false;

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
                          else
                          {
                              List<Token> token = new List<Token>();

                              if (line.Text.Trim().IndexOf("--@") == 0)
                              {
                                  var x = Regex.Match(line.Text, "[\\w ]+=[ \\w]+");

                                  if (x.Success)
                                  {
                                      token = LanguageService.Lexer.Tokenize(new SourceText(x.Value).TextReader);
                                  }

                              }
                              else
                              {
                                  token = LanguageService.Lexer.Tokenize(line.TextReader);
                              }
                              if (token.Count > 3)
                              {
                                  if (token[0].Kind == SyntaxKind.Identifier)
                                  {
                                      CheckDef(token);

                                  }
                                  else if (token[0].Kind == SyntaxKind.LocalKeyword)
                                  {
                                      ChecklocalDef(token);
                                  }

                              }
                          }


  
                      }

                      task = null;
                  });

                task.Start();

            }
        }


        private Type GetRx(string txt,out bool IsStatic)
        {
            IsStatic = true;

            if (txt.IndexOf('=') > 0)
            {
                var x = txt.Split(new char[] { '=' }, StringSplitOptions.RemoveEmptyEntries);
                if (x.Length > 0)
                    txt = x[x.Length - 1];
            }

            var tp= Regex.Matches(txt, "\\b[\\w]+\\b");

            if (tp.Count == 0)
                return null;
            else
            {
                 string root= tp[0].Value;

                if (TypeDir.ContainsKey(root))
                {
                    Type currentType = TypeDir[root];

                    for (int i = 1; i < tp.Count; i++)
                    {
                        string member = tp[i].Value;
                        re:
                        MemberInfo[] info=currentType.GetMember(member);

                        if(info.Length>0)
                        {
                            foreach (var item in info)
                            {
                                if(item.MemberType==MemberTypes.Property)
                                {
                                    IsStatic = false;
                                    currentType = (item as PropertyInfo).PropertyType;
                                    break;
                                }
                                else if (item.MemberType == MemberTypes.Method)
                                {
                                    IsStatic = false;
                                    currentType = (item as MethodInfo).ReturnType;
                                    break;
                                }
                                else if (item.MemberType == MemberTypes.Field)
                                {
                                    IsStatic = false;
                                    currentType = (item as FieldInfo).FieldType;
                                    break;
                                }
                                else if (item.MemberType == MemberTypes.Event)
                                {
                                    IsStatic = false;
                                    currentType = (item as EventInfo).EventHandlerType;
                                    break;
                                }
                            }
                        }
                        else
                        {
                            if (currentType.BaseType != null)
                            {
                                currentType = currentType.BaseType;
                                goto re;
                            }
                        }
                    }

                    return currentType;
                }
                else if(defDir.ContainsKey(root))
                {
                    IsStatic = false;

                    Type currentType = defDir[root];

                    for (int i = 1; i < tp.Count; i++)
                    {
                        string member = tp[i].Value;

                      re:
                        MemberInfo[] info = currentType.GetMember(member);

                        if (info.Length > 0)
                        {
                            foreach (var item in info)
                            {
                                if (item.MemberType == MemberTypes.Property)
                                {
                                    IsStatic = false;
                                    currentType = (item as PropertyInfo).PropertyType;
                                    break;
                                }
                                else if (item.MemberType == MemberTypes.Method)
                                {
                                    IsStatic = false;
                                    currentType = (item as MethodInfo).ReturnType;
                                    break;
                                }
                                else if (item.MemberType == MemberTypes.Field)
                                {
                                    IsStatic = false;
                                    currentType = (item as FieldInfo).FieldType;
                                    break;
                                }
                                else if (item.MemberType == MemberTypes.Event)
                                {
                                    IsStatic = false;
                                    currentType = (item as EventInfo).EventHandlerType;
                                    break;
                                }
                            }
                        }
                        else
                        {
                            if(currentType.BaseType!=null)
                            {
                                currentType = currentType.BaseType;
                                goto re;
                            }
                        }
                    }

                    return currentType;
                }
                else
                    return null;
            }

        }

        private string GetGetConstructorsStr(Type type)
        {
            var consturct = type.GetConstructors();

            string txt = "";

            foreach (var item in consturct)
            {
                txt +="\r\n"+item.ToString();

            }

            return txt;

        }


        private string GetGetMethodstr(Type type)
        {
            var method = type.GetMethods();

            string txt = "";

            foreach (var item in method)
            {
                txt += "\r\n" + item.ToString();

            }

            return txt;

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
                else if(defDir.ContainsKey(cmd))
                {
                    foreach (var member in GetTypeMembers(defDir[cmd], false))
                    {
                        completions.Add(new Completion(member.Name, member.Name, member.type + "\r\n" + member.info, null, ""));
                    }
                }
                else
                {
                    
                    var ret = GetRx(linetxt,out isStatic);

                    if(ret!=null)
                    {
                        foreach (var member in GetTypeMembers(ret, isStatic))
                        {
                            completions.Add(new Completion(member.Name, member.Name, member.type + "\r\n" + member.info, null, ""));
                        }
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
                else if (defDir.ContainsKey(cmd))
                {
                    foreach (var member in GetTypeMethods(defDir[cmd], false))
                    {
                        completions.Add(new Completion(member.Name, member.Name, member.type + "\r\n" + member.info, null, ""));
                    }
                }
                else
                {
                    var ret = GetRx(linetxt, out isStatic);

                    if (ret != null)
                    {
                        foreach (var member in GetTypeMethods(ret, isStatic))
                        {
                            completions.Add(new Completion(member.Name, member.Name, member.type + "\r\n" + member.info, null, ""));
                        }
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

                    var dirlist = TypeDir.Where(p => p.Key.ToLower().IndexOf(cmd) == 0);

                    if (dirlist.Count() > 0)
                    {
                        foreach (var str in dirlist)
                        {
                            completions.Add(new Completion(str.Key,str.Key,str.Value.ToString()+ GetGetConstructorsStr(str.Value), null,null));
                        }
                    }

                    var deflist = defDir.Where(p => p.Key.ToLower().IndexOf(cmd) == 0);

                    if (deflist.Count() > 0)
                    {
                        foreach (var str in deflist)
                        {
                            completions.Add(new Completion(str.Key, str.Key, str.Value.ToString() + GetGetConstructorsStr(str.Value), null, null));
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

