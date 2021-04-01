using System;

namespace XSOverlay_VRChat_Parser.Helpers
{
    [AttributeUsage(AttributeTargets.Property)]
    public class Annotation : Attribute
    {
        public string description;
        public string groupDescription;
        public bool startsGroup;
        public Annotation(string _description, bool _startsGroup = false, string _groupDescription = "")
        {
            this.description = _description;
            this.startsGroup = _startsGroup;
            this.groupDescription = _groupDescription;
        }
    }
}
