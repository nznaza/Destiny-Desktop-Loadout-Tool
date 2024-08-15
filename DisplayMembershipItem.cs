using DotNetBungieAPI.Models;
using System;
using System.Windows.Media.Imaging;

namespace DestinyLoadoutTool
{
    internal class DisplayMembershipItem
    {
        public required string Name { get; set; }
        public required BitmapImage Icon { get; set; }
        public BungieMembershipType MembershipType { get; set; }
        public long MembershipId { get; set; }
    }
}