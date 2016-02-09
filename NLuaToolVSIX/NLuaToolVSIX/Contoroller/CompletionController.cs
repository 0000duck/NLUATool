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
using System.ComponentModel.Composition;
using System.Diagnostics;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Utilities;
using Microsoft.VisualStudio;
using System.Windows;
using System.Runtime.InteropServices;
using LanguageService.Formatting;
using LanguageService.Formatting.Options;
using LanguageService.Shared;

namespace OokLanguage
{
    #region Command Filter

    [Export(typeof(IVsTextViewCreationListener))]
    [ContentType("luacode!")]
    [TextViewRole(PredefinedTextViewRoles.Interactive)]
    internal sealed class VsTextViewCreationListener : IVsTextViewCreationListener
    {
        [Import]
        IVsEditorAdaptersFactoryService AdaptersFactory = null;

        [Import]
        ICompletionBroker CompletionBroker = null;

        public void VsTextViewCreated(IVsTextView textViewAdapter)
        {
            IWpfTextView view = AdaptersFactory.GetWpfTextView(textViewAdapter);
            Debug.Assert(view != null);

            CommandFilter filter = new CommandFilter(view, CompletionBroker);

            IOleCommandTarget next;
            textViewAdapter.AddCommandFilter(filter, out next);
            filter.Next = next;
        }
    }

    internal sealed class CommandFilter : IOleCommandTarget
    {
        ICompletionSession _currentSession;

        public CommandFilter(IWpfTextView textView, ICompletionBroker broker)
        {
            _currentSession = null;

            TextView = textView;
            Broker = broker;
            Settings = new UserSettings();
        }

        public IWpfTextView TextView { get; private set; }
        public ICompletionBroker Broker { get; private set; }
        public IOleCommandTarget Next { get; set; }

        private LanguageService.LuaFeatureContainer luaLuaFeature = new LanguageService.LuaFeatureContainer();

        private SourceTextCache txtCache = new SourceTextCache();
        public static UserSettings Settings { get; set; }

        private char GetTypeChar(IntPtr pvaIn)
        {
            return (char)(ushort)Marshal.GetObjectForNativeVariant(pvaIn);
        }

        public int Exec(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
        {
            bool handled = false;
            int hresult = VSConstants.S_OK;

            // 1. Pre-process
            if (pguidCmdGroup == VSConstants.VSStd2K)
            {
                switch ((VSConstants.VSStd2KCmdID)nCmdID)
                {
                    case VSConstants.VSStd2KCmdID.AUTOCOMPLETE:
                    case VSConstants.VSStd2KCmdID.COMPLETEWORD:
                        handled = StartSession();
                        break;
                    case VSConstants.VSStd2KCmdID.RETURN:
                        handled = Complete(false);
                        break;
                    case VSConstants.VSStd2KCmdID.TAB:
                        handled = Complete(true);
                        break;
                    case VSConstants.VSStd2KCmdID.CANCEL:
                        handled = Cancel();
                        break;
                    
                }
            }

            if (!handled)
                hresult = Next.Exec(pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);

            if (ErrorHandler.Succeeded(hresult))
            {
                if (pguidCmdGroup == VSConstants.VSStd2K)
                {
                    switch ((VSConstants.VSStd2KCmdID)nCmdID)
                    {
                        case VSConstants.VSStd2KCmdID.TYPECHAR:
                            char ch = GetTypeChar(pvaIn);

                            if(ch=='.'||ch==':'||ch=='=')
                            {
                                if (_currentSession != null)
                                    Cancel();
                            }
                            
                            if (ch != '\r'&&ch!='\n'&&ch!=';'&&ch!=' '&&ch!='}'&&ch!=')'&& ch!='('&&ch!='['&&ch!='{'&&ch!=']'&ch!='*')
                            {
                                StartSession();
                            }
                            else if(ch == '\r' || ch == '\n' || ch == ';' || ch == ' ' || ch == '{' || ch == '(' || ch == '[' || ch == ':')
                            {
                                Cancel();
                            }
                            else if (_currentSession != null)
                                Filter();
                            break;
                        case VSConstants.VSStd2KCmdID.BACKSPACE:
                            Filter();
                            break;
                        case VSConstants.VSStd2KCmdID.RETURN:
                            Format();
                            break;

                    }
                }
            }

            return hresult;
        }

        private void Format()
        {
            SnapshotPoint caret = this.TextView.Caret.Position.BufferPosition;

            Range range = new Range(0, caret.Snapshot.Length);

            List<DisableableRules> disabledRules = this.GetDisabledRules(Settings);
            FormattingOptions formattingOptions = GetFormattingOptions(Settings);
           
            List<TextEditInfo> edits = luaLuaFeature.Formatter.Format(txtCache.Get(caret.Snapshot), range, formattingOptions);

            using (ITextEdit textEdit = this.TextView.TextBuffer.CreateEdit())
            {
                foreach (TextEditInfo edit in edits)
                {
                    textEdit.Replace(edit.Start, edit.Length, edit.ReplacingWith);
                }

                textEdit.Apply();
            }
        }

        /// <summary>
        /// Narrow down the list of options as the user types input
        /// </summary>
        private void Filter()
        {
            if (_currentSession == null)
                return;

            if(TextView.Caret.Position.BufferPosition<=showPost)
            {
                showPost = 0;
                _currentSession.Dismiss();
                return;
            }

            _currentSession.SelectedCompletionSet.SelectBestMatch();
            _currentSession.SelectedCompletionSet.Recalculate();
        }

        /// <summary>
        /// Cancel the auto-complete session, and leave the text unmodified
        /// </summary>
        bool Cancel()
        {
            if (_currentSession == null)
                return false;

            _currentSession.Dismiss();

            return true;
        }

        /// <summary>
        /// Auto-complete text using the specified token
        /// </summary>
        bool Complete(bool force)
        {

          

            if (_currentSession == null)
                return false;

            if (!_currentSession.SelectedCompletionSet.SelectionStatus.IsSelected && !force)
            {
                _currentSession.Dismiss();
                return false;
            }
            else
            {
                _currentSession.Commit();
                return true;
            }
        }


        private int showPost = 0;

        /// <summary>
        /// Display list of potential tokens
        /// </summary>
        bool StartSession()
        {
            if (_currentSession != null)
                return false;

            SnapshotPoint caret = TextView.Caret.Position.BufferPosition;
            ITextSnapshot snapshot = caret.Snapshot;
           
            showPost = caret.Position;

            if (!Broker.IsCompletionActive(TextView))
            {
                _currentSession = Broker.CreateCompletionSession(TextView, snapshot.CreateTrackingPoint(caret, PointTrackingMode.Positive), true);
            }
            else
            {
                _currentSession = Broker.GetSessions(TextView)[0];
            }


           
            _currentSession.Dismissed += (sender, args) => _currentSession = null;
          
            _currentSession.Start();

            return true;
        }

   

        public int QueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText)
        {
            if (pguidCmdGroup == VSConstants.VSStd2K)
            {
                switch ((VSConstants.VSStd2KCmdID)prgCmds[0].cmdID)
                {
                    case VSConstants.VSStd2KCmdID.AUTOCOMPLETE:
                    case VSConstants.VSStd2KCmdID.COMPLETEWORD:
                        prgCmds[0].cmdf = (uint)OLECMDF.OLECMDF_ENABLED | (uint)OLECMDF.OLECMDF_SUPPORTED;
                        return VSConstants.S_OK;
                }
            }
            return Next.QueryStatus(pguidCmdGroup, cCmds, prgCmds, pCmdText);
        }

        private FormattingOptions GetFormattingOptions(UserSettings settings)
        {

            List<DisableableRules> disabledRules = this.GetDisabledRules(settings);

            FormattingOptions formattingOptions = new FormattingOptions(disabledRules, settings.TabSize, settings.IndentSize, settings.UsingTabs);

            return formattingOptions;
        }

        private List<DisableableRules> GetDisabledRules(UserSettings settings)
        {
            var disabledRules = new List<DisableableRules>();

            if (settings.AddSpacesOnInsideOfCurlyBraces != true)
            {
                disabledRules.Add(DisableableRules.SpaceOnInsideOfCurlyBraces);
            }

            if (settings.AddSpacesOnInsideOfParenthesis != true)
            {
                disabledRules.Add(DisableableRules.SpaceOnInsideOfParenthesis);
            }

            if (settings.AddSpacesOnInsideOfSquareBrackets != true)
            {
                disabledRules.Add(DisableableRules.SpaceOnInsideOfSquareBrackets);
            }

            if (settings.SpaceBetweenFunctionAndParenthesis != true)
            {
                disabledRules.Add(DisableableRules.SpaceBeforeOpenParenthesis);
            }

            if (settings.SpaceAfterCommas != true)
            {
                disabledRules.Add(DisableableRules.SpaceAfterCommas);
            }

            if (settings.SpaceBeforeAndAfterAssignmentInStatement != true)
            {
                disabledRules.Add(DisableableRules.SpaceBeforeAndAfterAssignmentForStatement);
            }

            if (settings.SpaceBeforeAndAfterAssignmentOperatorOnField != true)
            {
                disabledRules.Add(DisableableRules.SpaceBeforeAndAfterAssignmentForField);
            }

            if (settings.ForLoopAssignmentSpacing != true)
            {
                disabledRules.Add(DisableableRules.SpaceBeforeAndAfterAssignmentInForLoopHeader);
            }

            if (settings.ForLoopIndexSpacing != true)
            {
                disabledRules.Add(DisableableRules.NoSpaceBeforeAndAfterIndiciesInForLoopHeader);
            }

            if (settings.AddNewLinesToMultilineTableConstructors != true)
            {
                disabledRules.Add(DisableableRules.WrappingMoreLinesForTableConstructors);
            }

            if (settings.WrapSingleLineForLoops != true)
            {
                disabledRules.Add(DisableableRules.WrappingOneLineForFors);
            }

            if (settings.WrapSingleLineFunctions != true)
            {
                disabledRules.Add(DisableableRules.WrappingOneLineForFunctions);
            }

            if (settings.WrapSingleLineTableConstructors != true)
            {
                disabledRules.Add(DisableableRules.WrappingOneLineForTableConstructors);
            }

            if (settings.SpaceBeforeAndAfterBinaryOperations != true)
            {
                disabledRules.Add(DisableableRules.SpaceBeforeAndAfterBinaryOperations);
            }

            return disabledRules;
        }
    }

    #endregion
}