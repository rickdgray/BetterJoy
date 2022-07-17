using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using EvenBetterJoy.Domain;
using EvenBetterJoy.Models;
using EvenBetterJoy.Services;

namespace EvenBetterJoy
{
    public partial class _3rdPartyControllers : Form
    {
        readonly string path;

        readonly IDeviceService deviceService;

        public _3rdPartyControllers(IDeviceService deviceService)
        {
            this.deviceService = deviceService;
            //TODO: Ioptions inject path
            path = $"{Path.GetDirectoryName(AppContext.BaseDirectory)}\\3rdPartyControllers";
        }

        //TODO: why are there two constructors?
        public _3rdPartyControllers()
        {
            InitializeComponent();
            list_allControllers.HorizontalScrollbar = true; list_customControllers.HorizontalScrollbar = true;

            chooseType.Items.AddRange(new string[] { "Pro Controller", "Left Joycon", "Right Joycon" });

            chooseType.FormattingEnabled = true;
            group_props.Controls.Add(chooseType);
            group_props.Enabled = false;

            if (File.Exists(path))
            {
                using var file = new StreamReader(path);
                var line = string.Empty;
                while ((line = file.ReadLine()) != null && (line != string.Empty))
                {
                    string[] split = line.Split('|');
                    //won't break existing config file
                    string serial_number = "";
                    if (split.Length > 4)
                    {
                        serial_number = split[4];
                    }
                    list_customControllers.Items.Add(new SController(split[0], ushort.Parse(split[1]), ushort.Parse(split[2]), byte.Parse(split[3]), serial_number));
                }
            }

            CopyCustomControllers();
            RefreshControllerList();
        }

        public void CopyCustomControllers()
        {
            Program.thirdPartyCons.Clear();
            foreach (SController v in list_customControllers.Items)
            {
                Program.thirdPartyCons.Add(v);
            }
        }

        private static bool ContainsText(ListBox a, string manu)
        {
            foreach (SController v in a.Items)
            {
                if (v == null)
                    continue;
                if (v.name == null)
                    continue;
                if (v.name.Equals(manu))
                    return true;
            }
            return false;
        }

        private void RefreshControllerList()
        {
            list_allControllers.Items.Clear();
            IntPtr ptr = deviceService.EnumerateDevice(0x0, 0x0);
            IntPtr top_ptr = ptr;

            // Add device to list
            DeviceInfo enumerate;
            while (ptr != IntPtr.Zero)
            {
                enumerate = (DeviceInfo)Marshal.PtrToStructure(ptr, typeof(DeviceInfo));

                if (enumerate.serial_number == null)
                {
                    ptr = enumerate.next;
                    continue;
                }

                // TODO: try checking against interface number instead
                string name = enumerate.product_string + '(' + enumerate.vendor_id + '-' + enumerate.product_id + '-' + enumerate.serial_number + ')';
                if (!ContainsText(list_customControllers, name) && !ContainsText(list_allControllers, name))
                {
                    list_allControllers.Items.Add(new SController(name, enumerate.vendor_id, enumerate.product_id, 0, enumerate.serial_number));
                    // 0 type is undefined
                    Console.WriteLine("Found controller " + name);
                }

                ptr = enumerate.next;
            }
            deviceService.FreeDeviceList(top_ptr);
        }

        private void btn_add_Click(object sender, EventArgs e)
        {
            if (list_allControllers.SelectedItem != null)
            {
                list_customControllers.Items.Add(list_allControllers.SelectedItem);
                list_allControllers.Items.Remove(list_allControllers.SelectedItem);

                list_allControllers.ClearSelected();
            }
        }

        private void btn_remove_Click(object sender, EventArgs e)
        {
            if (list_customControllers.SelectedItem != null)
            {
                list_allControllers.Items.Add(list_customControllers.SelectedItem);
                list_customControllers.Items.Remove(list_customControllers.SelectedItem);

                list_customControllers.ClearSelected();
            }
        }

        private void btn_apply_Click(object sender, EventArgs e)
        {
            string sc = "";
            foreach (SController v in list_customControllers.Items)
            {
                sc += v.Serialise() + "\r\n";
            }
            File.WriteAllText(path, sc);
            CopyCustomControllers();
        }

        private void btn_applyAndClose_Click(object sender, EventArgs e)
        {
            btn_apply_Click(sender, e);
            Close();
        }

        private void _3rdPartyControllers_FormClosing(object sender, FormClosingEventArgs e)
        {
            btn_apply_Click(sender, e);
        }

        private void btn_refresh_Click(object sender, EventArgs e)
        {
            RefreshControllerList();
        }

        private void list_allControllers_SelectedValueChanged(object sender, EventArgs e)
        {
            if (list_allControllers.SelectedItem != null)
                tip_device.Show((list_allControllers.SelectedItem as SController).name, list_allControllers);
        }

        private void list_customControllers_SelectedValueChanged(object sender, EventArgs e)
        {
            if (list_customControllers.SelectedItem != null)
            {
                SController v = (list_customControllers.SelectedItem as SController);
                tip_device.Show(v.name, list_customControllers);

                chooseType.SelectedIndex = v.type - 1;

                group_props.Enabled = true;
            }
            else
            {
                chooseType.SelectedIndex = -1;
                group_props.Enabled = false;
            }
        }

        private void list_customControllers_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Y > list_customControllers.ItemHeight * list_customControllers.Items.Count)
                list_customControllers.SelectedItems.Clear();
        }

        private void list_allControllers_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Y > list_allControllers.ItemHeight * list_allControllers.Items.Count)
                list_allControllers.SelectedItems.Clear();
        }

        private void chooseType_SelectedValueChanged(object sender, EventArgs e)
        {
            if (list_customControllers.SelectedItem != null)
            {
                SController v = (list_customControllers.SelectedItem as SController);
                v.type = (byte)(chooseType.SelectedIndex + 1);
            }
        }
    }
}
