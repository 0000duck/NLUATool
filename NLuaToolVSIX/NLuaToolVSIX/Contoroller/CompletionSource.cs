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

        private static List<string> LocalDir = new List<string>();

        private static Dictionary<string, Type> TypeDir = new Dictionary<string, Type>();

        private static Dictionary<Type, List<string>> typeMembers = new Dictionary<Type, List<string>>();
        private static Dictionary<Type, List<string>> typeMethods = new Dictionary<Type, List<string>>();

        static OokCompletionSource()
        {
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


            LoadingNameSpace(typeof(string).Assembly,"System");

            
        }



        public static void LoadingNameSpace(string namespaces)
        {
            //var types= Assembly.LoadWithPartialName(namespaces);

           // if (types == null)
              var  types = typeof(string).Assembly;
            LoadingNameSpace(types, namespaces);
        }

        public static void LoadingNameSpace(Assembly assembly,string namespaces)
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


        private static List<string> GetTypeMethods(Type type,bool isstatic)
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


        private static List<string> GetTypeMembers(Type type,bool isStatic)
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
                        isStatic = false;
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
                        isStatic = false;
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

                    var applicableTo = snapshot.CreateTrackingSpan(new SnapshotSpan(start, triggerPoint), SpanTrackingMode.EdgeInclusive);

                    completionSets.Add(new CompletionSet("All", "All", applicableTo, completions, Enumerable.Empty<Completion>()));
                }
            }



          
        }

        public void Dispose()
        {
            _disposed = true;
        }
    }
}

