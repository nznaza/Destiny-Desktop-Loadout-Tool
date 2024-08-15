using DotNetBungieAPI.Models.Destiny;
using DotNetBungieAPI.Models.Destiny.Definitions.Classes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace DestinyLoadoutTool
{
    internal class DisplayCharacterItem
    {
        public required long CharacterId { get; set; }
        public required long MembershipId { get; set; }
        public required DestinyClass Class { get; set; }
        public required BitmapImage Icon { get; set; }
        public required string Name { get; set; }
        public required DateTime LastPlayed { get; set; }

    }
}
