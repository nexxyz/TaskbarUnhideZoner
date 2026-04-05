using TaskbarUnhideZoner.Models;
using TaskbarUnhideZoner.Services;
using TaskbarUnhideZoner.UI;

namespace TaskbarUnhideZoner.Tray;

internal sealed partial class TrayApp
{
    private ToolStripMenuItem BuildDelayMenu()
    {
        var menu = new ToolStripMenuItem("Trigger Delay");
        var quick = new ToolStripMenuItem("Quick") { CheckOnClick = true };
        var normal = new ToolStripMenuItem("Default") { CheckOnClick = true };
        var longDelay = new ToolStripMenuItem("Long") { CheckOnClick = true };
        var customSeparator = new ToolStripSeparator();
        var custom = new ToolStripMenuItem("Custom (from config)") { Enabled = false, CheckOnClick = false };

        void UpdateChecks()
        {
            var selected = TriggerDelayPresets.DetectExact(_runtime.Config);
            var isCustom = selected == null;
            quick.Checked = selected == DelayPreset.Quick;
            normal.Checked = selected == DelayPreset.Default;
            longDelay.Checked = selected == DelayPreset.Long;
            custom.Checked = isCustom;
            customSeparator.Visible = isCustom;
            custom.Visible = isCustom;
        }

        void SetPreset(DelayPreset preset)
        {
            if (_initializing)
            {
                return;
            }

            _runtime.SetDelayPreset(preset);
            UpdateChecks();
        }

        quick.Click += (_, _) => SetPreset(DelayPreset.Quick);
        normal.Click += (_, _) => SetPreset(DelayPreset.Default);
        longDelay.Click += (_, _) => SetPreset(DelayPreset.Long);

        UpdateChecks();

        menu.DropDownItems.Add(quick);
        menu.DropDownItems.Add(normal);
        menu.DropDownItems.Add(longDelay);
        menu.DropDownItems.Add(customSeparator);
        menu.DropDownItems.Add(custom);
        return menu;
    }

    private ToolStripMenuItem BuildTriggerAssistMenu()
    {
        var menu = new ToolStripMenuItem("Trigger Assist");
        var off = new ToolStripMenuItem("Off") { CheckOnClick = true };
        var low = new ToolStripMenuItem("Low") { CheckOnClick = true };
        var medium = new ToolStripMenuItem("Medium") { CheckOnClick = true };
        var strong = new ToolStripMenuItem("Strong") { CheckOnClick = true };
        var customSeparator = new ToolStripSeparator();
        var custom = new ToolStripMenuItem("Custom (from config)") { Enabled = false, CheckOnClick = false };

        void UpdateChecks()
        {
            var selectedPreset = TriggerAssistPresets.DetectExact(_runtime.Config.Trigger.Assist);
            var isCustom = selectedPreset == null;
            off.Checked = selectedPreset == TriggerAssistPreset.Off;
            low.Checked = selectedPreset == TriggerAssistPreset.Low;
            medium.Checked = selectedPreset == TriggerAssistPreset.Medium;
            strong.Checked = selectedPreset == TriggerAssistPreset.Strong;
            custom.Checked = isCustom;
            customSeparator.Visible = isCustom;
            custom.Visible = isCustom;
        }

        void SetPreset(TriggerAssistPreset preset)
        {
            if (_initializing)
            {
                return;
            }

            _runtime.SetTriggerAssistPreset(preset);
            UpdateChecks();
        }

        off.Click += (_, _) => SetPreset(TriggerAssistPreset.Off);
        low.Click += (_, _) => SetPreset(TriggerAssistPreset.Low);
        medium.Click += (_, _) => SetPreset(TriggerAssistPreset.Medium);
        strong.Click += (_, _) => SetPreset(TriggerAssistPreset.Strong);

        UpdateChecks();

        menu.DropDownItems.Add(off);
        menu.DropDownItems.Add(low);
        menu.DropDownItems.Add(medium);
        menu.DropDownItems.Add(strong);
        menu.DropDownItems.Add(customSeparator);
        menu.DropDownItems.Add(custom);
        return menu;
    }

    private ToolStripMenuItem BuildZoneMenu()
    {
        var menu = new ToolStripMenuItem("Select zone");

        menu.DropDownItems.Add(CreateEdgeItem("Select Top Edge...", EdgePosition.Top));
        menu.DropDownItems.Add(CreateEdgeItem("Select Bottom Edge...", EdgePosition.Bottom));
        menu.DropDownItems.Add(CreateEdgeItem("Select Left Edge...", EdgePosition.Left));
        menu.DropDownItems.Add(CreateEdgeItem("Select Right Edge...", EdgePosition.Right));
        menu.DropDownItems.Add(new ToolStripSeparator());

        var hotZoneItem = new ToolStripMenuItem("Select Hot Zone...");
        hotZoneItem.Click += (_, _) => SelectHotZone();
        menu.DropDownItems.Add(hotZoneItem);

        return menu;
    }

    private ToolStripMenuItem CreateEdgeItem(string label, EdgePosition edge)
    {
        var item = new ToolStripMenuItem(label);
        item.Click += (_, _) => SelectEdgeZone(edge);
        return item;
    }

    private void SelectEdgeZone(EdgePosition edge)
    {
        if (_initializing)
        {
            return;
        }

        var rect = EdgeZoneOverlayForm.SelectEdgeRectangle(edge, _runtime.Config.Trigger.Assist);
        if (rect == null)
        {
            return;
        }

        _runtime.SetEdgeZone(edge, rect.Value);
        _runtime.ReinitializeDetection();
        RefreshUiState();
    }

    private void SelectHotZone()
    {
        if (_initializing)
        {
            return;
        }

        var rect = HotZoneOverlayForm.SelectRectangle(_runtime.Config.Trigger.Assist);
        if (rect == null)
        {
            return;
        }

        _runtime.SetHotZone(rect.Value);
        _runtime.ReinitializeDetection();
        RefreshUiState();
    }
}
