﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ninlabs.attachables.Models
{
    public class Reminder
    {
        public Reminder()
        {

        }

        public long Id { get; set; }

        public AbstractCondition Condition { get; set; }
        //public String ConditionAsString
        //{
        //    get
        //    {
        //        return Condition.ToString();
        //    }
        //}

        public NotificationType NotificationType { get; set; }

        public String ReminderMessage { get; set; }

        public bool IsCompleted { get; set; }

        public DateTime CreatedOn { get; set; }
    }
}
