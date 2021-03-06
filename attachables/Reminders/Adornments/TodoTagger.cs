﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.Text.Tagging;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Language.Intellisense;
using System.Collections.ObjectModel;
using System.Windows.Media;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Formatting;
using System.Diagnostics;
using ninlabs.attachables;
using ninlabs.attachables.Reminders.Adornments.Actions;
using ninlabs.attachables.Util;
using ninlabs.attachables.Reminders.Adornments;

namespace TodoArdornment
{
    public class TodoTagger : ITagger<TodoGlyphTag>
    {
        public event EventHandler<Microsoft.VisualStudio.Text.SnapshotSpanEventArgs> TagsChanged;
        public static Regex todoLineRegex = new Regex(@"\/\/!?\s*@?(TODO|FIXME|FIX|XXX)\s*(BY)?\b", RegexOptions.IgnoreCase);

        ITextView _textView;

        internal TodoTagger(ITextView textView)
        {
            _textView = textView;
            _textView.LayoutChanged += OnLayoutChanged;
            //_textView.MouseHover += _textView_MouseHover;
            _textView.Caret.PositionChanged += Caret_PositionChanged;
        }

        private void Caret_PositionChanged(object sender, CaretPositionChangedEventArgs e)
        {
            if (_textView.TextSnapshot.GetLineNumberFromPosition(e.NewPosition.BufferPosition) != _textView.TextSnapshot.GetLineNumberFromPosition(e.OldPosition.BufferPosition))
            {
                var point = e.NewPosition.BufferPosition;
                //var span = e.View.GetTextElementSpan(point);
                var span = point.GetContainingLine().Extent;

                //var old = _textView.GetTextViewLineContainingBufferPosition(e.OldPosition.BufferPosition);
                //var list = old.GetAdornmentTags(old.IdentityTag);

                // for new position
                RaiseTagsChanged(span);
                // for old position ( which doesn't have caret, so no actions will show )
                RaiseTagsChanged(e.OldPosition.BufferPosition.GetContainingLine().Extent);
            }

        }

        void _textView_MouseHover(object sender, MouseHoverEventArgs e)
        {
            var point = new SnapshotPoint(e.View.TextSnapshot, e.Position);
            //var span = e.View.GetTextElementSpan(point);
            var span = point.GetContainingLine().Extent;
            if (TagsChanged != null)
            {
                TagsChanged(this, new SnapshotSpanEventArgs(span));
            }
        }

        // TODO Investigate ITrackingPoint for updating TODO state. (edit).
        // TODO by Friday See if can use tracking to update line number of reminder.

        /*
         *   SnapshotPoint? point = textView.BufferGraph.MapDownToFirstMatch(
        new SnapshotPoint(textView.TextSnapshot, e.Position),
        PointTrackingMode.Positive,
        snapshot => textBuffers.Contains(snapshot.TextBuffer),
        PositionAffinity.Predecessor
      );
      if ( point != null ) {
        ITrackingPoint triggerPoint = point.Value.Snapshot.CreateTrackingPoint(
          point.Value.Position, PointTrackingMode.Positive);
        if ( provider.QuickInfoBroker.IsQuickInfoActive(textView) ) {
          session = provider.QuickInfoBroker.TriggerQuickInfo(textView, triggerPoint, true);
        }
      }*/

        void createTrackingPointTest(SnapshotPoint point, ITextView textView)
        { 
            if ( point != null ) 
            {
                ITrackingPoint triggerPoint = point.Snapshot.CreateTrackingPoint(
                    point.Position, PointTrackingMode.Positive);

                //var updatedPoint = triggerPoint.GetPoint( textView.TextSnapshot );
            }

            //SnapshotPoint? point = textView.BufferGraph.MapDownToFirstMatch(
            //    new SnapshotPoint(textView.TextSnapshot, e.Position),
            //    PointTrackingMode.Positive,
            //    snapshot => textView.BufferGraph.GetTextBuffers( null ).Contains(snapshot.TextBuffer),
            //    PositionAffinity.Predecessor
            //);
            //if ( provider.QuickInfoBroker.IsQuickInfoActive(textView) ) {
            //  session = provider.QuickInfoBroker.TriggerQuickInfo(textView, triggerPoint, true);

        }

        internal void RaiseTagsChanged(SnapshotSpan span)
        {
            if (TagsChanged != null)
            {
                TagsChanged(this, new SnapshotSpanEventArgs(span));
            }
        }

        private void OnLayoutChanged(object sender, TextViewLayoutChangedEventArgs e)
        {
            foreach (var span in e.NewOrReformattedSpans)
            {
                if (TagsChanged != null)
                {
                    TagsChanged(this, new SnapshotSpanEventArgs(span));
                }
            }
        }

        public IEnumerable<ITagSpan<TodoGlyphTag>> GetTags(Microsoft.VisualStudio.Text.NormalizedSnapshotSpanCollection spans)
        {
            foreach (var span in spans)
            {
                String text = span.GetText();
                var match = todoLineRegex.Match(text);
                if (match.Success)
                {
                    var point = span.Start.Add(match.Index);
                    var spanNew = new SnapshotSpan(span.Snapshot, new Span(point.Position, match.Length));
                    var hasDueBy = false;
                    if (match.Groups.Count == 2 && match.Groups[1].Value.ToLower() == "by" )
                    {
                        hasDueBy = true;
                    }

                    ITextViewLine line = null;
                    DateTime? dueDate = null;
                    string friendly = null;
                    try
                    {
                        line = _textView.Caret.ContainingTextViewLine;
                        if (hasDueBy)
                        {
                            var dateSpan = new SnapshotSpan(span.Snapshot, new Span(point.Position + match.Length, span.Length - match.Length));
                            var str = dateSpan.GetText();

                            DateMatcher matcher = new DateMatcher();
                            dueDate = matcher.FromString(str, out friendly);
                            
                            // Bail if there isn't a good date found (should give visual feedback to dev).
                            if (!dueDate.HasValue)
                                continue;
                        }
                    }
                    catch( Exception ex )
                    {
                        continue;
                    }
                    var actions = new ReadOnlyCollection<SmartTagActionSet>(new SmartTagActionSet[]{}.ToList());
                    if (line != null && 
                        //_textView.Caret.ContainingTextViewLine.ContainsBufferPosition(span.Start)
                        //true
                        _textView.TextSnapshot.GetLineNumberFromPosition(_textView.Caret.Position.BufferPosition) ==
                        span.Start.GetContainingLine().LineNumber
                        )
                    {
                         actions = GetSmartTagActions(spanNew, dueDate, friendly);
                    }
                    yield return new TagSpan<TodoGlyphTag>(spanNew, 
                        new TodoGlyphTag(SmartTagType.Ephemeral, actions)
                    );
                }
            }
        }

        private ReadOnlyCollection<SmartTagActionSet> GetSmartTagActions(SnapshotSpan span, DateTime? dueDate, string friendly)
        {
            ITrackingSpan trackingSpan = span.Snapshot.CreateTrackingSpan(span, SpanTrackingMode.EdgeInclusive);

            var filePath = CurrentFilePathRaw();

            var attachActionList = new List<ISmartTagAction>();
            attachActionList.Add(new AttachAction(trackingSpan, _textView, this, "Attach here", CurrentFilePath(), filePath));
            attachActionList.Add(new AttachAction(trackingSpan, _textView, this, "Attach everywhere", "", filePath));

            var whenActionList = new List<ISmartTagAction>();
            if( dueDate.HasValue )
            {
                var todoNote = DueByAction.ExtractTodoMessage(span.Start.GetContainingLine().GetText());

                // Look up if Todo Note exists and is incomplete
                var reminder = AttachablesPackage.Manager.FindReminderByProperties(todoNote, filePath, false);
                if (reminder == null )
                {
                    string actionTitle = "Due on " + dueDate.Value.ToShortDateString();
                    if (friendly != null)
                    {
                        actionTitle = string.Format("Due on {0} ({1})", friendly, dueDate.Value.ToShortDateString());
                    }
                    whenActionList.Add(new DueByAction(trackingSpan, this, actionTitle, dueDate.Value, friendly, filePath));
                }
                else
                {
                    // Provide Complete Option
                    whenActionList.Add(new CompleteDueByAction(trackingSpan, this, "Mark Complete", filePath, reminder));
                }
            }
            whenActionList.Add(new WhenAction(trackingSpan, this, "Show next day", TimeSpan.FromDays(1), filePath));
            whenActionList.Add(new WhenAction(trackingSpan, this, "Show next week", TimeSpan.FromDays(7), filePath));

            // list of action sets...
            var actionSetList = new List<SmartTagActionSet>();
            actionSetList.Add(new SmartTagActionSet(attachActionList.AsReadOnly()));
            actionSetList.Add(new SmartTagActionSet(whenActionList.AsReadOnly()));

            return actionSetList.AsReadOnly();
        }

        private string CurrentFilePath()
        {
            try
            {
                return "file;" + CurrentPositionHelper.GetCurrentFile();
            }
            catch (Exception ex)
            {
                return "";
            }
        }

        private string CurrentFilePathRaw()
        {
            try
            {
                return CurrentPositionHelper.GetCurrentFile();
            }
            catch (Exception ex)
            {
                return "";
            }
        }

    }
}
