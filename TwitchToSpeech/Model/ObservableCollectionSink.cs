using Serilog;
using Serilog.Configuration;
using Serilog.Core;
using Serilog.Events;
using System;
using System.Collections.ObjectModel;
using System.Windows.Threading;

namespace TwitchToSpeech.Model
{
    public class ObservableCollectionSink : ILogEventSink
    {
        private readonly IFormatProvider _formatProvider;
        private readonly ObservableCollection<string> _collection;
        private readonly Dispatcher _dispatcher;

        public ObservableCollectionSink(IFormatProvider formatProvider, ObservableCollection<string> collection, Dispatcher dispatcher)
        {
            _formatProvider = formatProvider;
            _collection = collection;
            _dispatcher = dispatcher;
        }

        public void Emit(LogEvent logEvent)
        {
            _dispatcher.Invoke(() => _collection.Add(logEvent.RenderMessage(_formatProvider)));
        }
    }

    public static class ObservableCollectionSinkExtensions
    {
        public static LoggerConfiguration ObservableCollection(
              this LoggerSinkConfiguration loggerConfiguration,
              ObservableCollection<string> collection,
              Dispatcher dispatcher,
              IFormatProvider formatProvider = null)
        {
            return loggerConfiguration.Sink(new ObservableCollectionSink(formatProvider, collection, dispatcher));
        }
    }
}
