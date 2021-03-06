﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Jacobi.Vst.Core;
using Jacobi.Vst.Interop.Host;
using NAudio.Wave;

namespace SimplyHost
{
    public class HostController
    {
        private VstPluginContext _plugin;
        private HostCommandStub _host;

        private readonly MainForm _form;
        private WaveOut _audioPlayer;
        private AudioGenerator _audioGenerator;
        private int _oktava;

        public HostController(MainForm form)
        {
            _form = form;
        }

        private VstPluginContext OpenPlugin(string pluginPath)
        {
                _host = new HostCommandStub();

                var ctx = VstPluginContext.Create(pluginPath, _host);

                ctx.Set("PluginPath", pluginPath);
                ctx.Set("HostCmdStub", _host);

                ctx.PluginCommandStub.SetSampleRate(1000 * _host.GetSampleRate());

                ctx.PluginCommandStub.Open();
	            
				return ctx;
        }

        public void Start()
        {
            const string extension = ".net.vstdll";
            var pluginName = Directory.GetFiles(Directory.GetCurrentDirectory(), "*" + extension, SearchOption.AllDirectories).FirstOrDefault();
            if (pluginName == null)
                throw new Exception("No plugin in directoy.");

            pluginName = pluginName.Replace(extension, ".dll");
            _plugin = OpenPlugin(pluginName);
            if (_plugin != null)
            {
                _form.Text = string.Format("{0} {1} {2} {3}",
                    _plugin.PluginCommandStub.GetEffectName(),
                    _plugin.PluginCommandStub.GetProductString(),
                    _plugin.PluginCommandStub.GetVendorString(),
                    _plugin.PluginCommandStub.GetVendorVersion());

                _plugin.PluginCommandStub.MainsChanged(true);

                Rectangle wndRect;
                if (_plugin.PluginCommandStub.EditorGetRect(out wndRect))
                {
                    var panelSize = _form.GetSizeFromClientSize(new Size(wndRect.Width, wndRect.Height));
                    panelSize.Height += _form.FMenu.Height;
                    _form.Size = panelSize;

                    _plugin.PluginCommandStub.EditorOpen(_form.PluginPanel.Handle);
                }
                else
                {
                    FillParametersForm();
                }

                FillProgramsList();

                _plugin.PluginCommandStub.MainsChanged(false);

                _audioGenerator = new AudioGenerator(_host, _plugin);
                _audioPlayer = new WaveOut();
                _audioPlayer.DesiredLatency = 110;

                _audioPlayer.Init(_audioGenerator);
                _audioPlayer.Play();
            }

            if (_plugin == null)
            {
                MessageBox.Show("No plugin loaded.");
                _form.Close();
            }
        }

        private void FillParametersForm()
        {
            _form.PluginPanel.AutoScroll = true;
            for (int i = 0; i < _plugin.PluginInfo.ParameterCount; ++i)
            {
                var editor = new ParameterEditor(i, _plugin.PluginCommandStub);
                editor.Location = new Point(0, i * editor.Height);
                _form.PluginPanel.Controls.Add(editor);
            }
        }

        private void FillProgramsList()
        {
            for (int i = 0; i < _plugin.PluginInfo.ProgramCount; ++i)
            {
                var localI = i;
                var item = new ToolStripMenuItem(_plugin.PluginCommandStub.GetProgramNameIndexed(localI));
                item.Click += (sender, args) =>
                {
                    foreach (var menuItem in _form.programsToolStripMenuItem.DropDownItems.Cast<ToolStripMenuItem>())
                        menuItem.Checked = false;

                    item.Checked = true;
                    _plugin.PluginCommandStub.SetProgram(localI);
                };
                
                _form.programsToolStripMenuItem.DropDownItems.Add(item);
            }

            (_form.programsToolStripMenuItem.DropDownItems[0] as ToolStripMenuItem).Checked = true;
        }

        public void Finish()
        {
            if (_plugin != null)
            {
                _audioPlayer.Stop();
                _audioPlayer.Dispose();

                _plugin.PluginCommandStub.EditorClose();
                _plugin.Dispose();
                _plugin = null;
            }
        }

        private readonly HashSet<Keys> _pressedKeys = new HashSet<Keys>(); 

	    public void KeyDown(KeyEventArgs keyEventArgs)
        {
            if (_pressedKeys.Contains(keyEventArgs.KeyCode))
                return;

	        _pressedKeys.Add(keyEventArgs.KeyCode);

            int note = GetKeyNote(keyEventArgs);
			if (note >= 0)
				SendEventNotePress(note + _oktava * 12);
		}

		public void KeyUp(KeyEventArgs keyEventArgs)
		{
		    _pressedKeys.Remove(keyEventArgs.KeyCode);

            int note = GetKeyNote(keyEventArgs);
		    if (note >= 0)
		    {
		        SendEventNoteRelease(note + _oktava * 12);
		    }
            else if (keyEventArgs.KeyCode == Keys.PageUp)
            {
                _oktava++;
            }
            else if (keyEventArgs.KeyCode == Keys.PageDown)
            {
                _oktava--;
            }
        }

	    private void SendEventNotePress(int note)
	    {
		    SendEventNote(144, note, 127);
	    }

		private void SendEventNoteRelease(int note)
		{
			SendEventNote(128, note, 127);
		}

		private void SendEventNote(byte cmd, int note, byte velocity)
		{
			_plugin.PluginCommandStub.ProcessEvents(new VstEvent[]
			{
				new VstMidiEvent(0, 0, 0, new byte[] {cmd, (byte)(60 + note), velocity, 0}, 0, 0)
			});
		}

		private static int GetKeyNote(KeyEventArgs keyEventArgs)
		{
			int note = -1;

			switch (keyEventArgs.KeyCode)
			{
				case Keys.Q:
					note = 0; break;
				case Keys.W:
					note = 2; break;
				case Keys.E:
					note = 4; break;
				case Keys.R:
					note = 5; break;
				case Keys.T:
					note = 7; break;
				case Keys.Y:
					note = 9; break;
				case Keys.U:
					note = 11; break;
				case Keys.I:
					note = 12; break;
			}

			return note;
		}

        public void SetBypass(bool bypass)
        {
            _plugin.PluginCommandStub.SetBypass(bypass);
        }

	    public void CopyParameters()
	    {
		    var parameterStrings = new List<string>();
		    for (int i = 0; i < _plugin.PluginInfo.ParameterCount; ++i)
		    {
			    parameterStrings.Add(string.Format("{0,-10}{1}",
				    _plugin.PluginCommandStub.GetParameterName(i),
				    _plugin.PluginCommandStub.GetParameterDisplay(i)));
		    }

		    var presetString = string.Join(Environment.NewLine, parameterStrings);
			Clipboard.SetText(presetString);
			
            //MessageBox.Show(presetString);
	    }
    }
}
