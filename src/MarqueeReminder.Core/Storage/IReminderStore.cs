using System.Collections.Generic;
using MarqueeReminder.Core.Models;

namespace MarqueeReminder.Core.Storage
{
    public interface IReminderStore
    {
        IList<Reminder> Load();

        void Save(IEnumerable<Reminder> reminders);
    }
}
