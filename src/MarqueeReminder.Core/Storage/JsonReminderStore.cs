using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Xml;
using MarqueeReminder.Core.Models;

namespace MarqueeReminder.Core.Storage
{
    /// <summary>Stores reminders in a JSON file, written atomically so a crash never leaves it half-written.</summary>
    public sealed class JsonReminderStore : IReminderStore
    {
        private static readonly DataContractJsonSerializer Serializer = new DataContractJsonSerializer(typeof(ReminderDocumentDto));

        private readonly string _filePath;

        public JsonReminderStore(string filePath)
        {
            _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        }

        public IList<Reminder> Load()
        {
            if (!File.Exists(_filePath))
            {
                return new List<Reminder>();
            }

            ReminderDocumentDto? document;
            try
            {
                // The JSON reader rejects a byte-order mark, which editors like Notepad may add.
                // ReadAllText detects and strips it; re-encode without one.
                var json = File.ReadAllText(_filePath);
                using (var stream = new MemoryStream(new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(json)))
                {
                    document = (ReminderDocumentDto?)Serializer.ReadObject(stream);
                }
            }
            catch (Exception ex) when (ex is SerializationException || ex is XmlException)
            {
                // Keep the unreadable file for inspection rather than overwriting the user's data.
                BackUpCorruptFile();
                return new List<Reminder>();
            }

            return (document?.Reminders ?? new List<ReminderDto>())
                .Select(ReminderDtoMapper.FromDto)
                .Where(reminder => reminder != null)
                .Select(reminder => reminder!)
                .ToList();
        }

        public void Save(IEnumerable<Reminder> reminders)
        {
            var document = new ReminderDocumentDto
            {
                Reminders = reminders.Select(ReminderDtoMapper.ToDto).ToList(),
            };

            var directory = Path.GetDirectoryName(Path.GetFullPath(_filePath));
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var tempPath = _filePath + ".tmp";
            using (var stream = File.Create(tempPath))
            using (var writer = JsonReaderWriterFactory.CreateJsonWriter(stream, Encoding.UTF8, ownsStream: false, indent: true))
            {
                Serializer.WriteObject(writer, document);
            }

            if (File.Exists(_filePath))
            {
                File.Replace(tempPath, _filePath, destinationBackupFileName: null);
            }
            else
            {
                File.Move(tempPath, _filePath);
            }
        }

        private void BackUpCorruptFile()
        {
            var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            File.Copy(_filePath, $"{_filePath}.corrupt-{stamp}", overwrite: true);
        }
    }
}
