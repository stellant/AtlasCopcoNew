using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Globalization;
using System.IO;
using MicroTorque;
using MicroTorque.Protocol;
using MicroTorque.Exceptions;

namespace AtlasCopco_ScrewDriver
{
    public partial class MainForm : Form
    {
        AsciiConnection mtConn = new AsciiConnection();
        connectionStatus connection = connectionStatus.CON_DISCONNECTED;
        Int32 cycleCounter = 0;
        static readonly Encoding mtEncoding = Encoding.GetEncoding(28591);
        string filepath = "";
        
   
        enum connectionStatus
        {
            CON_DISCONNECTED = 0,
            CON_USB,
        }

        public MainForm()
        {
            InitializeComponent();
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            //Enable - disable buttons in forms while loading
            button_connect.Enabled = true;
            button_disconnect.Enabled = false;
            textbox_status.Enabled = false;
            button_disconnect.Enabled = false;
            textbox_status.Enabled = false;
            textbox_filename.Enabled = false;

            MTCom.DeviceInfo[] devices = MTCom.GetDeviceList();
            connection = connectionStatus.CON_DISCONNECTED;

            foreach (MTCom.DeviceInfo devInfo in devices)
            {
                if (devInfo.IfType == MTCom.ComInterface.MT_IF_USB &&
                    devInfo.DevStatus == MTCom.DeviceStatus.MT_DEVICE_READY)
                {
                    switch (devInfo.DevType)
                    {
                        case MTCom.DeviceType.MT_DEVICE_MTF400_A:
                        case MTCom.DeviceType.MT_DEVICE_MTF400_B:
                        case MTCom.DeviceType.MT_DEVICE_MTF400_D:
                        case MTCom.DeviceType.MT_DEVICE_MT_G4:
                            combobox_usbdevices.Items.Add(devInfo);
                            break;

                        default:
                            break;
                    }
                }
            }

            if (combobox_usbdevices.Items.Count > 0)
                combobox_usbdevices.SelectedIndex = 0;
        }

        private void button_connect_Click(object sender, EventArgs e)
        {
            MTCom.DeviceInfo devInfo = combobox_usbdevices.SelectedItem as MTCom.DeviceInfo;

            if (textbox_frequency.Text == "" || textbox_filename.Text == "")
            {
                MessageBox.Show("Frequency or Filename cannot be empty.");
                return;
            }

            //If the range is in 1 to 32767
            else
            {
                int value_frequency = -1;

                try
                {
                    value_frequency = int.Parse(textbox_frequency.Text);
                }

                catch (Exception string_message)
                {
                    MessageBox.Show("Exception :{0}", string_message.Message);
                    return;
                }

                if (value_frequency > 0 && value_frequency <= 32767)
                {
                    if (devInfo != null && !mtConn.IsOpen)
                    {
                        try
                        {
                            mtConn.Open(devInfo);
                            groupbox_devices.Enabled = false;
                            groupbox_save.Enabled = false;
                            textbox_status.Text = "Connected via USB";
                            connection = connectionStatus.CON_USB;
                            
                            //If the connection is successful
                            button_connect.Enabled = false;
                            button_disconnect.Enabled = true;
                            
                            timer1.Interval = int.Parse(textbox_frequency.Text);
                            timer1.Start();
                        }
                        catch (MTException ex)
                        {
                            MessageBox.Show(ex.Message);
    
                            //Any exception occurs, close the connection
                            textbox_status.Text = ex.Message;
                            groupbox_save.Enabled = true;
                            groupbox_devices.Enabled = true;
                            button_disconnect.Enabled = false;
                            button_connect.Enabled = true;
                            connection = connectionStatus.CON_DISCONNECTED;
                            timer1.Stop();
                        }
                    }
                }
                else
                {
                    MessageBox.Show("Please enter a valid value of Frequency (1-32767).");
                    return;
                }
            }
        }

        private void button_browse_Click(object sender, EventArgs e)
        {
            SaveFileDialog saveFileDialog1 = new SaveFileDialog();
            saveFileDialog1.InitialDirectory = @"C:\";
            saveFileDialog1.Title = "Save Sensor Log Files";
            saveFileDialog1.CheckPathExists = true;
            saveFileDialog1.DefaultExt = "CSV";
            saveFileDialog1.Filter = "CSV files (*.csv)|*.log|All files (*.*)|*.*";
            saveFileDialog1.FilterIndex = 2;
            saveFileDialog1.RestoreDirectory = true;

            if (saveFileDialog1.ShowDialog() == DialogResult.OK)
            {
                filepath = saveFileDialog1.FileName;
                char[] delims = { '\\' };
                string[] sub_string = filepath.Split(delims);
                textbox_filename.Text = sub_string[sub_string.Length - 1];
            }
        }

        private void button_disconnect_Click(object sender, EventArgs e)
        {
            if (mtConn.IsOpen)
                mtConn.Close();

            //On disconnect - make the filename
            textbox_filename.Text = "";
            textbox_status.Text = "";
            textbox_status.Text = "Disconnected";
            groupbox_save.Enabled = true;
            groupbox_devices.Enabled = true;
            button_connect.Enabled = true;
            button_disconnect.Enabled = false;
            connection = connectionStatus.CON_DISCONNECTED;
            timer1.Stop();
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            if (mtConn.IsOpen)
            {
                Int32 cycleCounterTmp = 0;

                try
                {
                    string response = mtConn.SendWaitReply("IC", 2);
                    string getCycleCounterString = response.Substring(4, 8);
                    cycleCounterTmp = Convert.ToInt32(getCycleCounterString, 16);

                    //If it is screwing
                    if (cycleCounter < cycleCounterTmp)
                    {
                        cycleCounter = cycleCounterTmp;
                        textbox_status.Text = String.Format("Cycle counter is currently {0}", cycleCounter);
                        saveTraceToFile();
                    }

                    //If it is unscrewing

                    else
                    {
                        //Save it to the file
                        textbox_status.Text = String.Format("Unscrew Operation");
                        saveTraceToFile();
                    }
                }

                catch (MTException ex)
                {
                    if (mtConn.IsOpen)
                        mtConn.Close();

                    MessageBox.Show("Exception occured while Connection");
                    textbox_status.Text = ex.Message;
                    groupbox_save.Enabled = true;
                    groupbox_devices.Enabled = true;
                    button_disconnect.Enabled = false;
                    button_connect.Enabled = true;
                    connection = connectionStatus.CON_DISCONNECTED;
                    timer1.Stop();
                }

                catch (FormatException)
                {
                    MessageBox.Show("Format Exception occured");

                }
                catch (OverflowException)
                {
                    MessageBox.Show("OverFlow Exception occured");
                }
            }
       }

        private string reference_time()
        {
            //Get the difference between the UTC and current time
            int hour_difference = DateTime.UtcNow.Hour - DateTime.Now.Hour;
            int minute_difference = DateTime.UtcNow.Minute - DateTime.Now.Minute;
            int second_difference = DateTime.UtcNow.Second - DateTime.Now.Second;
            int millisecond_difference = DateTime.UtcNow.Millisecond - DateTime.Now.Millisecond;

            return "TZ " + hour_difference;
        }
        
        private void saveTraceToFile()
        {
            int sampleRate;
            MTCom.Unit torqueUnit;

            //Get the data got from performing last measurement tightening/screwing
            MTCom.TracePoint[] tracePoints = mtConn.GetTracePoints(0, out sampleRate, out torqueUnit);
            
            //Get the Data Sets from MicroTorque Devices
            byte[] reply_readset = new byte[4096];
            reply_readset = mtConn.Receive(1000);

            //Data sets contained in Strings
            string received_dataset = System.Text.Encoding.UTF8.GetString(reply_readset);
            
            if (received_dataset.Length == 0 || received_dataset == "")
            {
                textbox_status.Text = "No Data Set received";
            }
            
            try
            {
                System.Globalization.CultureInfo cultureInfo =
                    System.Globalization.CultureInfo.GetCultureInfo("en-US");

               //Get the time and append the timestamp to filename
               filepath += "_"+DateTime.Now.Year + DateTime.Now.Month + DateTime.Now.Day + DateTime.Now.Hour + DateTime.Now.Minute + DateTime.Now.Second + DateTime.Now.Millisecond + reference_time();

                using (StreamWriter outputFile = File.CreateText(filepath))
                { 
                    //Write all the measurement data to one file for one operation
                    for (int j = 0; j < tracePoints.Length; j++)
                    {
                                   
                        //Add the headers 
                        outputFile.WriteLine("No."+","+"Time" + "," + "Angle" + "," + "Torque" + "," + "SampleRate" + "," + "Units");
                        
                        //Write Measurement data to the file
                        outputFile.WriteLine("{0},{1},{2},{4},{5}",j,DateTime.Now.Year + DateTime.Now.Month + DateTime.Now.Day + DateTime.Now.Hour + DateTime.Now.Minute + DateTime.Now.Second + DateTime.Now.Millisecond + reference_time(), tracePoints[j].angle, tracePoints[j].torque, sampleRate, torqueUnit.ToString());
                        
                        //string outputLine = String.Format(cultureInfo, "Timestamp = {0} Torque Angle = {1}, Torque = {2}, SampleRate = {3},Units = {4}", localtime.Day + "." + localtime.Month + "." + localtime.Year + " " + localtime.Hour + ":" + localtime.Minute + ":" + localtime.Second + ":" + localtime.Millisecond, tracePoints[j].angle, tracePoints[j].torque, sampleRate, torqueUnit.ToString());
                   }

                    //Write the Data Sets to a file
                    outputFile.WriteLine(received_dataset);
                }
                               
                textbox_status.Text = String.Format("{0} data points (angle,torque) were written to file: \n\r {1}", tracePoints.Length, filepath);
            }
            catch
            {
                MessageBox.Show("Problem accessing file or folder");
            }
        }

    }
}
