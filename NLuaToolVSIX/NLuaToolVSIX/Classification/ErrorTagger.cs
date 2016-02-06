using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Internal.VisualStudio.Shell;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Tagging;
using LanguageService;
using System.Threading;
using Microsoft;
using System.Text.RegularExpressions;

namespace OokLanguage
{
    internal sealed class ErrorTagger : DisposableObject, ITagger<ErrorTag>
    {
        private ITextBuffer buffer;
        
        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;
        private CancellationTokenSource cancellationTokenSource;


        LuaFeatureContainer luaContainer = new LuaFeatureContainer();


        public ErrorTagger(ITextBuffer buffer)
        {
          
            this.buffer = buffer;         
            this.buffer.Changed += this.OnBufferChanged;
        }

        public IEnumerable<ITagSpan<ErrorTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            if (spans.Count == 0)
            {
                yield break;
            }

            // It is possible that we could be asked for tags for any text buffer,
            // not necessarily the one text buffer that this tagger knows about.
            if (spans[0].Snapshot.TextBuffer != this.buffer)
            {
                yield break;
            }

            ITextSnapshot textSnapshot = spans[0].Snapshot.TextBuffer.CurrentSnapshot;
            SourceText sourceText = new SourceText(textSnapshot.GetText());
            IReadOnlyList<ParseError> errors = luaContainer.DiagnosticsProvider.GetDiagnostics(sourceText);

            if (errors.Count == 0)
            {
                yield break;
            }

            SnapshotSpan spanOfEntireSpansCollection = new SnapshotSpan(spans[0].Start, spans[spans.Count - 1].End).TranslateTo(textSnapshot, SpanTrackingMode.EdgeExclusive);
            int entireEndIncludingLineBreak = spanOfEntireSpansCollection.End.GetContainingLine().EndIncludingLineBreak;

            for (int errorIndex = 0; errorIndex < errors.Count; errorIndex++)
            {
                ParseError error = errors[errorIndex];

                if (error.Start > entireEndIncludingLineBreak)
                {
                    continue;
                }

                ITextSnapshotLine line = spanOfEntireSpansCollection.Snapshot.GetLineFromPosition(error.Start);
                int errorStart = (error.Start <= line.End || error.Start == line.EndIncludingLineBreak) ? error.Start : line.End;

                // Determine whether error intersects requested span
                if (((errorStart + error.Length) < spanOfEntireSpansCollection.Start) || (errorStart > spanOfEntireSpansCollection.End))
                {
                    continue;
                }

                SnapshotSpan newSnapshotSpan = CreateSnapshotSpan(textSnapshot, errorStart, error.Length);

                yield return new TagSpan<ErrorTag>(newSnapshotSpan, new ErrorTag(PredefinedErrorTypeNames.SyntaxError, error.Message));
            }

           
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


        protected override void DisposeManagedResources()
        {
            if (this.buffer != null)
            {
                this.buffer.Changed -= this.OnBufferChanged;
            }

            base.DisposeManagedResources();
        }

        private void OnBufferChanged(object sender, TextContentChangedEventArgs e)
        {
            if (this.cancellationTokenSource != null)
            {
                this.cancellationTokenSource.Cancel();
            }

            this.cancellationTokenSource = new CancellationTokenSource();
            this.UpdateErrorsWithDelay(e.After, this.cancellationTokenSource.Token);
        }

        private void UpdateErrorsWithDelay(ITextSnapshot snapshot, CancellationToken token)
        {
            Task.Run(async () =>
            {
                await Task.Delay(875).WithoutCancellation();

                if (token.IsCancellationRequested)
                {
                    return;
                }

                this.TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(new SnapshotSpan(snapshot, 0, snapshot.Length)));
            }, token);
        }
    }

    internal static class TaskExtensions
    {
        internal static Task WithoutCancellation(this Task task)
        {
            Requires.NotNull(task, nameof(task));

            var tcs = new TaskCompletionSource<object>();
            task.ContinueWith(
                t =>
                {
                    if (t.IsFaulted)
                    {
                        tcs.SetException(t.Exception);
                    }
                    else
                    {
                        tcs.SetResult(null);
                    }
                },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);

            return tcs.Task;
        }
    }
}
