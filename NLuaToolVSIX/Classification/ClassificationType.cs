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

using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace OokLanguage
{
    internal static class OrdinaryClassificationDefinition
    {
        #region Type definition

        /// <summary>
        /// Defines the "ookExclamation" classification type.
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("_keyword")]
        internal static ClassificationTypeDefinition ookExclamation = null;

        [Export(typeof(ClassificationTypeDefinition))]
        [Name("_string")]
        internal static ClassificationTypeDefinition stringExclamation = null;

        [Export(typeof(ClassificationTypeDefinition))]
        [Name("_note")]
        internal static ClassificationTypeDefinition noteExclamation = null;

        [Export(typeof(ClassificationTypeDefinition))]
        [Name("_call")]
        internal static ClassificationTypeDefinition callExclamation = null;

        #endregion



    }
}
