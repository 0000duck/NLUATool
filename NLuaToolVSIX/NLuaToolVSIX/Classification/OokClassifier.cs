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
    using LanguageService;
    using LanguageService.Classification;
    using Microsoft.VisualStudio.Language.StandardClassification;
    using Microsoft.VisualStudio.Text.Adornments;


    [Export(typeof(ITaggerProvider))]
    [ContentType("luacode!")]
    [TagType(typeof(ClassificationTag))]
    internal sealed class OokClassifierProvider : ITaggerProvider
    {

        [Export]
        [Name("luacode!")]
        [BaseDefinition("code")]
        internal static ContentTypeDefinition OokContentType = null;

        [Export]
        [FileExtension(".lua")]
        [ContentType("luacode!")]
        internal static FileExtensionToContentTypeDefinition OokFileType = null;

        [Import]
        internal IClassificationTypeRegistryService ClassificationTypeRegistry = null;

        [Import]
        internal IBufferTagAggregatorFactoryService aggregatorFactory = null;

      

        public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag
        {

            ITagAggregator<OokTokenTag> ookTagAggregator = 
                                            aggregatorFactory.CreateTagAggregator<OokTokenTag>(buffer);

            return new OokClassifier(buffer, ookTagAggregator, ClassificationTypeRegistry) as ITagger<T>;
        }
    }


    [Export(typeof(ITaggerProvider))]
    [TagType(typeof(ErrorTag))]
    [ContentType("luacode!")]
    internal sealed class ErrorTaggerProvider : ITaggerProvider
    {

        [Import]
        internal IBufferTagAggregatorFactoryService aggregatorFactory = null;
        public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag
        {

            Func<ErrorTagger> errorTaggerCreator = () =>
            {
                ErrorTagger tagger = new ErrorTagger(buffer);

                return tagger;
            };

            return buffer.Properties.GetOrCreateSingletonProperty<ErrorTagger>(errorTaggerCreator) as ITagger<T>;
        }
    }


    internal sealed class OokClassifier : ITagger<ClassificationTag>
    {
        ITextBuffer _buffer;
        ITagAggregator<OokTokenTag> _aggregator;
        IDictionary<OokTokenTypes, IClassificationType> _ookTypes;

        LanguageService.LuaFeatureContainer luaaFeature = new LuaFeatureContainer();

        /// <summary>
        /// Construct the classifier and define search tokens
        /// </summary>
        internal OokClassifier(ITextBuffer buffer, 
                               ITagAggregator<OokTokenTag> ookTagAggregator, 
                               IClassificationTypeRegistryService typeService
                                )
        {
           
            _buffer = buffer;
            _aggregator = ookTagAggregator;
            _ookTypes = new Dictionary<OokTokenTypes, IClassificationType>();
            _ookTypes[OokTokenTypes.keyword] = typeService.GetClassificationType("_keyword");
            _ookTypes[OokTokenTypes.stringchar] = typeService.GetClassificationType("_string");
            _ookTypes[OokTokenTypes.node] = typeService.GetClassificationType("_note");
            
        }

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged
        {
            add { }
            remove { }
        }


        internal static SnapshotSpan CreateSnapshotSpan(ITextSnapshot snapshot, int position, int length)
        {
            // Assume a bogus (negative) position to be at the end.
            if (position < 0)
            {
                position = snapshot.Length;
            }

            position = Math.Min(position, snapshot.Length);
            length = Math.Max(0, Math.Min(length, snapshot.Length - position));

            return new SnapshotSpan(snapshot, position, length);
        }

        /// <summary>
        /// Search the given span for any instances of classified tags
        /// </summary>
        public IEnumerable<ITagSpan<ClassificationTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {      
            

            foreach (var tagSpan in _aggregator.GetTags(spans))
            {
                var tagSpans = tagSpan.Span.GetSpans(spans[0].Snapshot);
                yield return
                    new TagSpan<ClassificationTag>(tagSpans[0],
                                                   new ClassificationTag(_ookTypes[tagSpan.Tag.type]));
            }


       
           
        }
    }
}
