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

namespace OokLanguage
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.Composition;
    using Microsoft.VisualStudio.Text;
    using Microsoft.VisualStudio.Text.Classification;
    using Microsoft.VisualStudio.Text.Editor;
    using Microsoft.VisualStudio.Text.Tagging;
    using Microsoft.VisualStudio.Utilities;
    using System.Text.RegularExpressions;

    [Export(typeof(ITaggerProvider))]
    [ContentType("luacode!")]
    [TagType(typeof(OokTokenTag))]
    internal sealed class OokTokenTagProvider : ITaggerProvider
    {

        public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag
        {
            return new OokTokenTagger(buffer) as ITagger<T>;
        }
    }

    public class OokTokenTag : ITag 
    {
        public OokTokenTypes type { get; private set; }

        public OokTokenTag(OokTokenTypes type)
        {
            this.type = type;
        }
    }

    internal sealed class OokTokenTagger : ITagger<OokTokenTag>
    {

        ITextBuffer _buffer;
        IDictionary<string, OokTokenTypes> _ookTypes;

        internal OokTokenTagger(ITextBuffer buffer)
        {
            _buffer = buffer;
            _ookTypes = new Dictionary<string, OokTokenTypes>();

            _ookTypes["import"] = OokTokenTypes.keyword;
            _ookTypes["and"] = OokTokenTypes.keyword;
            _ookTypes["break"] = OokTokenTypes.keyword;
            _ookTypes["do"] = OokTokenTypes.keyword;
            _ookTypes["else"] = OokTokenTypes.keyword;
            _ookTypes["elseif"] = OokTokenTypes.keyword;
            _ookTypes["end"] = OokTokenTypes.keyword;
            _ookTypes["false"] = OokTokenTypes.keyword;
            _ookTypes["true"] = OokTokenTypes.keyword;
            _ookTypes["for"] = OokTokenTypes.keyword;
            _ookTypes["function"] = OokTokenTypes.keyword;
            _ookTypes["if"] = OokTokenTypes.keyword;
            _ookTypes["in"] = OokTokenTypes.keyword;
            _ookTypes["local"] = OokTokenTypes.keyword;
            _ookTypes["nil"] = OokTokenTypes.keyword;
            _ookTypes["not"] = OokTokenTypes.keyword;
            _ookTypes["or"] = OokTokenTypes.keyword;
            _ookTypes["repeat"] = OokTokenTypes.keyword;
            _ookTypes["return"] = OokTokenTypes.keyword;
            _ookTypes["then"] = OokTokenTypes.keyword;
            _ookTypes["until"] = OokTokenTypes.keyword;
            _ookTypes["while"] = OokTokenTypes.keyword;
            _ookTypes["_string"] = OokTokenTypes.stringchar;
            _ookTypes["_node"] = OokTokenTypes.node;
            _ookTypes["print"] = OokTokenTypes.keyword;
            _ookTypes["require"] = OokTokenTypes.keyword;
            

        }

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged
        {
            add { }
            remove { }
        }

        public IEnumerable<ITagSpan<OokTokenTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {

            foreach (SnapshotSpan curSpan in spans)
            {
                ITextSnapshotLine containingLine = curSpan.Start.GetContainingLine();

                string linetxt = containingLine.GetText().ToLower();
                string[] tokens = linetxt.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                int curLoc = containingLine.Start.Position;
                int tk = 0;

                foreach (string ookToken in tokens)
                {

                    string[] valuesp = ookToken.Split(new char[] { '\t', '\n', '\r', ' ', '(', ')', '{', '}', ';', ',' }, StringSplitOptions.RemoveEmptyEntries);

                    foreach (var item in valuesp)
                    {
                        if (_ookTypes.ContainsKey(item))
                        {
                            int startpost = linetxt.IndexOf(item, tk);
                            if (startpost >= 0)
                            {
                                tk = startpost + item.Length;
                                var tokenSpan = new SnapshotSpan(curSpan.Snapshot, new Span(curLoc + startpost, item.Length));
                                if (tokenSpan.IntersectsWith(curSpan))
                                    yield return new TagSpan<OokTokenTag>(tokenSpan,
                                                                          new OokTokenTag(_ookTypes[item]));
                            }
                            else
                            {
                                tk += item.Length + 1;
                            }
                        }
                        else
                        {
                            tk += item.Length + 1;
                        }
                    }
                }

                MatchCollection rx = Regex.Matches(linetxt, "\"([^\"]+)\"|\'([^\']+)\'");

                foreach (Match item in rx)
                {
                    if (item.Success)
                    {
                        var tokenSpan = new SnapshotSpan(curSpan.Snapshot, new Span(curLoc + item.Index, item.Length));
                        if (tokenSpan.IntersectsWith(curSpan))
                            yield return new TagSpan<OokTokenTag>(tokenSpan,
                                                                  new OokTokenTag(_ookTypes["_string"]));
                    }
                }


                rx = Regex.Matches(linetxt, "--([^\n^\r]+)");

                foreach (Match item in rx)
                {
                    if (item.Success)
                    {
                        var tokenSpan = new SnapshotSpan(curSpan.Snapshot, new Span(curLoc + item.Index, item.Length));
                        if (tokenSpan.IntersectsWith(curSpan))
                            yield return new TagSpan<OokTokenTag>(tokenSpan,
                                                                  new OokTokenTag(_ookTypes["_node"]));
                    }
                }   

                rx = Regex.Matches(containingLine.GetText(), "(?<=import[ ,\t]).+");

                foreach (Match item in rx)
                {
                    if (item.Success)
                    {
                        string val = item.Value.Trim(' ', '\t','"');
                        OokCompletionSource.LoadingNameSpace(val);
                    }
                }
            }
            
        }
    }
}
