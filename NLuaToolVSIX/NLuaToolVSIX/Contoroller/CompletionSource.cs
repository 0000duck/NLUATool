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

    class OokCompletionSource : ICompletionSource
    {
        private ITextBuffer _buffer;
        private bool _disposed = false;

        private  List<string> LocalDir = new List<string>();

        private  Dictionary<string, Type> TypeDir = new Dictionary<string, Type>();
        private  Dictionary<string, Assembly> AssDir = new Dictionary<string, Assembly>();

        private  Dictionary<Type, List<string>> typeMembers = new Dictionary<Type, List<string>>();
        private  Dictionary<Type, List<string>> typeMethods = new Dictionary<Type, List<string>>();
   

    

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


        private  List<string> GetTypeMethods(Type type,bool isstatic)
        {
            if (!typeMethods.ContainsKey(type))
            {
                List<string> tmp = new List<string>();
             
                if (type.IsClass)
                {
                    if (isstatic)
                    {
                        foreach (var item in (from p in type.GetMethods()
                                              where p.IsStatic && p.IsPublic
                                              select p.Name))
                        {

                            if (item.IndexOf("get_") == 0 || item.IndexOf("set_") == 0)
                            {
                                continue;
                            }
                            else
                            {
                                if (!tmp.Contains(item))
                                    tmp.Add(item);
                            }

                        }
                    }else
                    {
                        foreach (var item in (from p in type.GetMethods()
                                              where p.IsStatic==false && p.IsPublic
                                              select p.Name))
                        {

                            if (item.IndexOf("get_") == 0 || item.IndexOf("set_") == 0)
                            {
                                continue;
                            }
                            else
                            {
                                if (!tmp.Contains(item))
                                    tmp.Add(item);
                            }

                        }
                    }

                }

                tmp.Sort();

                typeMethods[type] = tmp;
                return tmp;
            }
            else
            {
                return typeMethods[type];
            }
        }


        private  List<string> GetTypeMembers(Type type,bool isStatic)
        {
            if (!typeMembers.ContainsKey(type))
            {
                List<string> tmp = new List<string>();

                if (type.IsEnum)
                    tmp.AddRange(type.GetEnumNames());
                else if (type.IsClass)
                {
                   
                    foreach (var item in (from p in type.GetMembers()
                                          select p.Name))
                    {


                        if (item.IndexOf("get_") == 0 || item.IndexOf("set_") == 0)
                        {
                            string bx = item.Substring(4, item.Length - 4);
                            if (!tmp.Contains(bx))
                                tmp.Add(bx);
                        }
                        else
                        {
                            if (!tmp.Contains(item))
                                tmp.Add(item);
                        }

                    }


                    foreach (var item in (from p in type.GetProperties()
                                          select p.Name))
                    {

                        if (!tmp.Contains(item))
                            tmp.Add(item);

                    }

                    if (isStatic)
                    {
                        foreach (var item in (from p in type.GetFields()
                                              where p.IsStatic && p.IsPublic
                                              select p.Name))
                        {

                            if (!tmp.Contains(item))
                                tmp.Add(item);

                        }
                    }else
                    {
                        foreach (var item in (from p in type.GetFields()
                                              where p.IsStatic==false && p.IsPublic
                                              select p.Name))
                        {

                            if (!tmp.Contains(item))
                                tmp.Add(item);

                        }
                    }
                }

                tmp.Sort();

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
                        TypeDir.Clear();
                        typeMembers.Clear();
                        typeMethods.Clear();

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
           
            foreach (var line in _buffer.CurrentSnapshot.Lines)
            {
                string txt = line.GetText();

                if (txt.IndexOf("import")>=0)
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

                    foreach (var str in GetTypeMembers(TypeDir[cmd],isStatic))
                    {
                        completions.Add(new Completion(str));
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

                    foreach (var str in GetTypeMethods(TypeDir[cmd], isStatic))
                    {
                        completions.Add(new Completion(str));
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

