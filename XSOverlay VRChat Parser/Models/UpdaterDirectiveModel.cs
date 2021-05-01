using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XSOverlay_VRChat_Parser.Models
{
    public class UpdaterDirectiveModel
    {
        public bool DoUpdateParser { get; set; }
        public string UpdatePath { get; set; }
        public string ParserPath { get; set; }
    }
}
