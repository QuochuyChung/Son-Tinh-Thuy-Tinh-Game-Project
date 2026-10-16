using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace SonTinhThuyTinh.Dialogue.Editor
{
    public class VoicePickerDropdown : AdvancedDropdown
    {
        readonly Action<string> onPicked;
        readonly Dictionary<AdvancedDropdownItem, string> itemToVoice = new Dictionary<AdvancedDropdownItem, string>();

        public VoicePickerDropdown(Action<string> onPicked) : base(new AdvancedDropdownState())
        {
            this.onPicked = onPicked;
            minimumSize = new Vector2(460, 480);
        }

        protected override AdvancedDropdownItem BuildRoot()
        {
            itemToVoice.Clear();
            var root = new AdvancedDropdownItem("All voices (" + VoiceCatalog.Count + ")");
            IReadOnlyList<VoiceInfo> list = VoiceCatalog.Voices;
            var groups = new Dictionary<string, AdvancedDropdownItem>();
            var order = new List<string>();

            foreach (VoiceInfo v in list)
            {
                string locale = string.IsNullOrEmpty(v.Locale) ? "other" : v.Locale;
                string label = locale + (string.IsNullOrEmpty(v.LocaleName) ? "" : " - " + v.LocaleName);
                if (!groups.TryGetValue(label, out AdvancedDropdownItem group))
                {
                    group = new AdvancedDropdownItem(label);
                    groups[label] = group;
                    order.Add(label);
                }
                string gender = string.IsNullOrEmpty(v.Gender) ? "" : "  (" + v.Gender + ")";
                var item = new AdvancedDropdownItem(v.ShortName + gender);
                group.AddChild(item);
                itemToVoice[item] = v.ShortName;
            }

            order.Sort(StringComparer.OrdinalIgnoreCase);
            foreach (string key in order)
                root.AddChild(groups[key]);
            return root;
        }

        protected override void ItemSelected(AdvancedDropdownItem item)
        {
            if (itemToVoice.TryGetValue(item, out string shortName))
            {
                if (!string.IsNullOrEmpty(shortName))
                    onPicked?.Invoke(shortName);
                return;
            }
            string label = item.name ?? "";
            int sep = label.IndexOf("  (", StringComparison.Ordinal);
            string candidate = sep > 0 ? label.Substring(0, sep) : label;
            if (VoiceCatalog.Contains(candidate))
                onPicked?.Invoke(candidate);
        }
    }
}
