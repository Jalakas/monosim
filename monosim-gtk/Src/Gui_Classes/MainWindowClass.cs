using System;
using System.Collections.Generic;
using System.IO;
using Glade;
using Gtk;
using Gdk;
using Pango;

using log4net;

using comexbase;
using monosimbase;


namespace monosimgtk
{


	public partial class MainWindowClass
	{
		// Attributes
		private string retStr = "";
		private string ATR = "";
		private ThreadNotify notify = null;
		private System.Threading.Thread simThread = null;


		#region Public Methods
		#endregion Public Methods
		#region Private methods


		/// <summary>
		/// Perform change of selected reader
		/// </summary>
		private void UpdateSelectedReader(string newSelReader)
		{
			GlobalObj.CloseConnection();
			GlobalObj.SelectedReader = newSelReader;
			StatusBar.Push(1, GlobalObj.LMan.GetString("selreader") + ": " + newSelReader);
		}


		private void NewContactsFile()
		{
			lstFileContacts.Clear();
			GlobalObjUI.FileContacts = new Contacts();
			GlobalObjUI.ContactsFilePath = "";
			UpdateFileControls(true);
		}


		private void OpenContactsFile()
		{
			GlobalObjUI.ContactsFilePath = "";

			// New dialog for select contacts file
			Gtk.FileChooserDialog FileBox = new Gtk.FileChooserDialog(GlobalObjUI.LMan.GetString("openfileact"),
											MainWindow,
											FileChooserAction.Open,
											GlobalObjUI.LMan.GetString("cancellbl"), Gtk.ResponseType.Cancel,
											GlobalObjUI.LMan.GetString("openlbl"), Gtk.ResponseType.Accept);

			// Filter for using only monosim files
			Gtk.FileFilter myFilter = new Gtk.FileFilter();
			myFilter.AddPattern("*.monosim");
			myFilter.Name = "monosim files";
			FileBox.AddFilter(myFilter);

			// Manage result of dialog box
			FileBox.Icon = Gdk.Pixbuf.LoadFromResource("monosim.png");
			int retFileBox = FileBox.Run();
			if ((ResponseType)retFileBox == Gtk.ResponseType.Accept)
			{
				// path of a right file returned
				GlobalObjUI.ContactsFilePath = FileBox.Filename;

				FileBox.Destroy();
				FileBox.Dispose();
			}
			else
			{
				// nothing returned
				FileBox.Destroy();
				FileBox.Dispose();
				return;
			}

			// Update gui
			UpdateFileControls(false);
			lstFileContacts.Clear();
			MainClass.GtkWait();

			try
			{
				GlobalObjUI.FileContacts = new Contacts();
				StreamReader sr = new StreamReader(GlobalObjUI.ContactsFilePath);
				string descRow = sr.ReadLine();
				string phoneRow = "";
				while (!sr.EndOfStream)
				{
					phoneRow = sr.ReadLine();
					// check for right values
					if (descRow.Trim() != "" && phoneRow.Trim() != "")
					{
						GlobalObjUI.FileContacts.SimContacts.Add(new Contact(descRow, phoneRow));
					}

					// read new contact description
					descRow = sr.ReadLine();
				}
				sr.Close();
				sr.Dispose();
				sr = null;

			}
			catch (Exception Ex)
			{
				log.Error("MainWindowClass::OpenContactsFile: " + Ex.Message + "\r\n" + Ex.StackTrace);
				MainClass.ShowMessage(MainWindow, "ERROR", Ex.Message, MessageType.Error);
				return;
			}

			// loop to append data readed from file
			foreach(Contact cnt in GlobalObjUI.FileContacts.SimContacts)
			{
				lstFileContacts.AppendValues(new string[]{cnt.Description, cnt.PhoneNumber});
			}

			UpdateFileControls(true);
		}


		/// <summary>
		/// Save file contacts on file.
		/// </summary>
		private void SaveContactsFile()
		{
			MessageDialog mdlg = null;
			string fileToSave = "";

			if (GlobalObjUI.ContactsFilePath != "")
			{
				mdlg = new MessageDialog(MainWindow,
										 DialogFlags.Modal,
										 MessageType.Question,
										 ButtonsType.YesNo,
										 GlobalObjUI.LMan.GetString("override") + "\r\n" +
										 Path.GetFileNameWithoutExtension(GlobalObjUI.ContactsFilePath));
				mdlg.TransientFor = MainWindow;
				mdlg.Title = MainClass.AppNameVer + " - " + GlobalObjUI.LMan.GetString("savefileact");
				ResponseType respType = (ResponseType)mdlg.Run();

				if (respType == ResponseType.Yes)
				{
					// override
					mdlg.Destroy();
					mdlg.Dispose();
					mdlg = null;

					WriteContactsOnFile(GlobalObjUI.ContactsFilePath, GlobalObjUI.FileContacts.SimContacts);
					return;
				}

				mdlg.Destroy();
				mdlg.Dispose();
				mdlg = null;

			}

			// select new file to save
			fileToSave = ChooseFileToSave(GlobalObjUI.LMan.GetString("savefileact"));
			if (fileToSave == "")
			{
				// no file selected
				return;
			}

			WriteContactsOnFile(fileToSave, GlobalObjUI.FileContacts.SimContacts);
			GlobalObjUI.ContactsFilePath = fileToSave;

		}


		private void SaveContactsFileOnSim()
		{
			// check for contacts description chars len
			string retCheck = GlobalObjUI.CheckAlphaCharsLen(GlobalObjUI.FileContacts);
			if (retCheck != "")
			{
				MainClass.ShowMessage(MainWindow, "ERROR",
					GlobalObjUI.LMan.GetString("maxlenexceeded")
					.Replace("'description'", "'<b>" + retCheck + "</b>'")
					+ "<b>" + GlobalObjUI.SimADNMaxAlphaChars.ToString() + "</b>", MessageType.Warning);
				return;
			}

			SelectWriteModeDialogClass swmdc =
				new SelectWriteModeDialogClass(MainWindow, GlobalObjUI.LMan.GetString("savefilesimact"));

			int retMode = swmdc.Show();

			if (retMode < 0)
			{
				// cancel button pressed
				return;
			}

			log.Debug("MainWindowClass::SaveContactsFileOnSim: SELECTED SIM WRITE MODE: " + retMode.ToString());
			if (retMode == 1)
			{
				WriteContactsOnSim(GlobalObjUI.FileContacts, true);
			}
			else
			{
				WriteContactsOnSim(GlobalObjUI.FileContacts, false);
			}
		}


		private void CloseContactsFile()
		{
			lstFileContacts.Clear();
			GlobalObjUI.ContactsFilePath = "";
			UpdateFileControls(false);
		}


		/// <summary>
		/// Perform sim card connection and contacts read.
		/// </summary>
		private void SimConnect()
		{
			MainClass.GtkWait();

			if (GlobalObj.IsPowered)
			{
				// Disconnect card if needed
				GlobalObj.CloseConnection();
			}

			// Connect to smartcard
			retStr = GlobalObj.AnswerToReset(ref ATR);

			// check for error
			if (retStr != "")
			{
				// error on answer to reset
				log.Error("MainWindowClass::SimConnect: " + retStr);
				MainClass.ShowMessage(MainWindow, "ERROR", retStr, MessageType.Error);
				return;
			}

			// read sim contacts and fill list
			retStr = GlobalObjUI.SelectSimContactsList();

			// check for error
			if (retStr != "")
			{
				if (retStr == GlobalObjUI.LMan.GetString("needpindisable"))
				{
					// Pin1 enabled
					MainClass.ShowMessage(MainWindow, "ERROR", retStr, MessageType.Error);
					EnableSimPinControl();
					return;
				}
				else
				{
					// error on reading contacts list
					GlobalObj.CloseConnection();
					MainClass.ShowMessage(MainWindow, "ERROR", retStr, MessageType.Error);
					return;
				}
			}

			ScanSimBefore();
			if (GlobalObjUI.SimADNVersion < 2) lstSimContacts.Clear();

			// Reset status values
			GlobalObjUI.SimADNStatus = 1;
			GlobalObjUI.SimADNPosition = 0;
			GlobalObjUI.SimADNError = "";

			// Start thread for reading process
			notify = new ThreadNotify(new ReadyEvent(ReadingUpdate));
			simThread = new System.Threading.Thread(new System.Threading.ThreadStart(GlobalObjUI.ReadSimContactsList));
			simThread.Start();
		}


		/// <summary>
		/// Start sim update thread
		/// </summary>
		private void SimUpdate(Contacts cnts, bool isAppend)
		{
			ScanSimBefore();

			// Reset status values
			GlobalObjUI.SimADNStatus = 1;
			GlobalObjUI.SimADNPosition = 0;
			GlobalObjUI.SimADNError = "";

			// Start thread for reading process
			notify = new ThreadNotify(new ReadyEvent(WritingUpdate));
			System.Threading.ThreadStart threadStart = delegate() {
				GlobalObjUI.WriteSimContactsList(cnts, isAppend);
			};
			simThread = new System.Threading.Thread(threadStart);
			simThread.Start();
		}


		/// <summary>
		/// Disconnect sim card from reader
		/// </summary>
		private void SimDisconnect()
		{
			GlobalObj.CloseConnection();
			UpdateSimControls(false);
			lstSimContacts.Clear();
			MainClass.GtkWait();
		}


		private void SimChangePin()
		{
			// check for Pin1 check attempts
			if (GlobalObjUI.SimPin1Attempts == 1)
			{
				// Pin1 one attempt
				MainClass.ShowMessage(MainWindow, GlobalObjUI.LMan.GetString("pinsimact"),
					GlobalObjUI.LMan.GetString("pinsimchk3"),MessageType.Warning);
				return;
			}
			else if (GlobalObjUI.SimPin1Attempts == 0)
			{
				// Pin1 no more attempt
				MainClass.ShowMessage(MainWindow, GlobalObjUI.LMan.GetString("pinsimact"),
					GlobalObjUI.LMan.GetString("pinsimchk4"),MessageType.Warning);
				return;
			}

			// Change Pin1 dialog
			ChangePinStatusDialogClass cpsdc = new ChangePinStatusDialogClass(MainWindow);
			string pin1 = cpsdc.Show();

			if (pin1 == null)
			{
				// cancel button pressed
				return;
			}

			// Perform Pin1 status change
			retStr = GlobalObjUI.SetPinStatus(!GlobalObjUI.SimPin1Status, pin1);

			if (retStr != "")
			{
				// error detected during Pin1 status change
				MainClass.ShowMessage(MainWindow, GlobalObjUI.LMan.GetString("pinsimact"),
					retStr,MessageType.Error);
				return;
			}

			// Pin1 status changed, reconnect sim now
			MainClass.ShowMessage(MainWindow, GlobalObjUI.LMan.GetString("pinsimact"),
					GlobalObjUI.LMan.GetString("pinsimdone"), MessageType.Info);

			// Force sim disconnect
			SimDisconnect();
		}


		private void SaveContactsSim()
		{
			// check for contacts description chars len
			string retCheck = GlobalObjUI.CheckAlphaCharsLen(GlobalObjUI.SimContacts);
			if (retCheck != "")
			{
				MainClass.ShowMessage(MainWindow, "ERROR",
					GlobalObjUI.LMan.GetString("maxlenexceeded")
					.Replace("'description'", "'<b>" + retCheck + "</b>'")
					+ "<b>" + GlobalObjUI.SimADNMaxAlphaChars.ToString() + "</b>", MessageType.Warning);
				return;
			}

			SelectWriteModeDialogClass swmdc =
				new SelectWriteModeDialogClass(MainWindow, GlobalObjUI.LMan.GetString("savesimact"));

			int retMode = swmdc.Show();

			if (retMode < 0)
			{
				// cancel button pressed
				return;
			}

			log.Debug("MainWindowClass::SaveContactsSim: SELECTED SIM WRITE MODE: " + retMode.ToString());
			if (retMode == 1)
			{
				WriteContactsOnSim(GlobalObjUI.SimContacts, true);
			}
			else
			{
				WriteContactsOnSim(GlobalObjUI.SimContacts, false);
			}
		}


		/// <summary>
		/// Save sim contacts on file
		/// </summary>
		private void SaveContactsSimOnFile()
		{
			string fileToSave = ChooseFileToSave(GlobalObjUI.LMan.GetString("savesimfileact"));
			if (fileToSave == "")
			{
				// no file selected
				return;
			}
			WriteContactsOnFile(fileToSave, GlobalObjUI.SimContacts.SimContacts);
		}


		private void DeleteContactsSim()
		{
			MessageDialog mdlg = new MessageDialog(MainWindow,
									 DialogFlags.Modal,
									 MessageType.Question,
									 ButtonsType.YesNo,
									 GlobalObjUI.LMan.GetString("suredeletesim"));
			mdlg.TransientFor = MainWindow;
			mdlg.Title = MainClass.AppNameVer + " - " + GlobalObjUI.LMan.GetString("deletesimact");
			ResponseType respType = (ResponseType)mdlg.Run();

			if (respType == ResponseType.Yes)
			{
				// override
				mdlg.Destroy();
				mdlg.Dispose();
				mdlg = null;

				// Delete sim
				ScanSimBefore();

				// Reset status values
				GlobalObjUI.SimADNStatus = 1;
				GlobalObjUI.SimADNPosition = 0;
				GlobalObjUI.SimADNError = "";

				// Start thread for reading process
				notify = new ThreadNotify(new ReadyEvent(WritingUpdate));
				simThread = new System.Threading.Thread(new System.Threading.ThreadStart(GlobalObjUI.DeleteAllSimContactsList));
				simThread.Start();

				return;
			}

			mdlg.Destroy();
			mdlg.Dispose();
			mdlg = null;

		}


		/// <summary>
		/// Updates during sim contacts reading
		/// </summary>
		private void ReadingUpdate()
		{
			PBar.Adjustment.Value = (double)GlobalObjUI.SimADNPosition;
			StatusBar.Push(1, GlobalObjUI.LMan.GetString("readcontact") +
							  GlobalObjUI.SimADNPosition.ToString("d3"));
			MainClass.GtkWait();


			if (GlobalObjUI.SimADNStatus == 3)
			{
				// End with errors
				MainClass.ShowMessage(MainWindow, "ERROR", GlobalObjUI.SimADNError, MessageType.Error);

				// Update gui widgets properties
				ScanSimAfter();

				// update gui widgets with results
				UpdateSimControls(false);
			}

			if (GlobalObjUI.SimADNStatus == 2)
			{
				// Extract contacts from records
				retStr = GlobalObjUI.FromRecordsToContacts();

				if (retStr != "")
				{
					// error detected
					MainClass.ShowMessage(MainWindow, "ERROR", retStr, MessageType.Error);

					// Update gui widgets properties
					ScanSimAfter();

					// update gui widgets with results
					UpdateSimControls(false);
				}
				else
				{
					// update ListView
					foreach(Contact cnt in GlobalObjUI.SimContacts.SimContacts)
					{
						lstSimContacts.AppendValues(new string[]{cnt.Description, cnt.PhoneNumber });
					}

					if (GlobalObjUI.SimADNVersion == 1)
					{
						log.Debug("MainWindowClass::ReadingUpdate: finished reading modern ADN, let's try ADN extended read");
						GlobalObjUI.SimADNVersion = 2;
						SimConnect();
					}
					else if (GlobalObjUI.SimADNVersion == 2)
					{
						GlobalObjUI.SimADNVersion = 1;
					}

					// Update gui widgets properties
					ScanSimAfter();

					// update gui widgets with results
					UpdateSimControls(true);

				}
			}

		}


		/// <summary>
		/// Updates during sim contacts writing
		/// </summary>
		private void WritingUpdate()
		{
			PBar.Adjustment.Value = (double)GlobalObjUI.SimADNPosition;
			StatusBar.Push(1, GlobalObjUI.LMan.GetString("writecontact") +
							  GlobalObjUI.SimADNPosition.ToString("d3"));
			MainClass.GtkWait();

			if (GlobalObjUI.SimADNStatus == 3)
			{
				// End with errors
				MainClass.ShowMessage(MainWindow, "ERROR", GlobalObjUI.SimADNError, MessageType.Error);
				//ScanSimAfter();
				SimConnect();
			}

			// check for sim write ended
			if (GlobalObjUI.SimADNStatus != 1)
			{
				// Update gui widgets properties
				//ScanSimAfter();
				SimConnect();
			}
		}


		/// <summary>
		/// Update sim widgets status
		/// </summary>
		private void UpdateSimControls(bool isSensitive)
		{
			MenuSimConnect.Sensitive = !isSensitive;
			MenuSimSaveSim.Sensitive = isSensitive;
			MenuSimSaveFile.Sensitive = isSensitive;
			MenuSimDeleteAll.Sensitive = isSensitive;
			MenuSimPin.Sensitive = isSensitive;
			MenuSimDisconnect.Sensitive = isSensitive;

			TbOpenSim.Sensitive = !isSensitive;
			TbSaveSimSim.Sensitive = isSensitive;
			TbSaveSimFile.Sensitive = isSensitive;
			TbChangePin.Sensitive = isSensitive;
			TbCloseSim.Sensitive = isSensitive;
			LstSimContacts.Sensitive = isSensitive;

			if (isSensitive)
			{
				// add iccid to frame label
				LblSim.Markup = "<b>" + GlobalObjUI.LMan.GetString("framesim") + "</b> [" +
					GlobalObjUI.SimICCID + " - size: " + GlobalObjUI.SimADNRecordCount.ToString() + "]";

				StatusBar.Push(1, GlobalObjUI.LMan.GetString("recordnoempty") +
								  GlobalObjUI.SimADNRecordNotEmpty.ToString());

				// check for File area enabled
				if (LstFileContacts.Sensitive)
				{
					MenuFileSaveSim.Sensitive = isSensitive;
					TbSaveSim.Sensitive = isSensitive;
				}
				else
				{
					MenuFileSaveSim.Sensitive = false;
					TbSaveSim.Sensitive = false;
				}

			}
			else
			{
				// clear frame label
				LblSim.Markup = "<b>" + GlobalObjUI.LMan.GetString("framesim") + "</b>";
				MenuFileSaveSim.Sensitive = isSensitive;
				TbSaveSim.Sensitive = isSensitive;
			}
		}


		/// <summary>
		/// Enable only change pin1 and disconnect controls
		/// </summary>
		private void EnableSimPinControl()
		{
			MenuSimConnect.Sensitive = false;
			TbOpenSim.Sensitive = false;
			MenuSimPin.Sensitive = true;
			TbChangePin.Sensitive = true;
			MenuSimDisconnect.Sensitive = true;
			TbCloseSim.Sensitive = true;
		}


		/// <summary>
		/// Update file widgets status
		/// </summary>
		private void UpdateFileControls(bool isSensitive)
		{
			MenuFileNew.Sensitive = !isSensitive;
			MenuFileOpen.Sensitive = !isSensitive;
			MenuFileSaveFile.Sensitive = isSensitive;
			MenuFileSaveSim.Sensitive = isSensitive;
			MenuFileClose.Sensitive = isSensitive;

			TbNew.Sensitive = !isSensitive;
			TbOpen.Sensitive = !isSensitive;
			TbSaveFile.Sensitive = isSensitive;
			TbSaveSim.Sensitive = isSensitive;
			TbClose.Sensitive = isSensitive;
			LstFileContacts.Sensitive = isSensitive;


			if (isSensitive)
			{
				// add filename to frame label
				LblFile.Markup = "<b>" + GlobalObjUI.LMan.GetString("framefile") + "</b>";
				if (GlobalObjUI.ContactsFilePath != "")
				{
					LblFile.Markup = "<b>" + GlobalObjUI.LMan.GetString("framefile") + "</b>" +
						" [" + Path.GetFileNameWithoutExtension(GlobalObjUI.ContactsFilePath) +
					" - size: " + GlobalObjUI.FileContacts.SimContacts.Count.ToString() + "]";
				}

				// check for sim power on
				if (LstSimContacts.Sensitive)
				{
					MenuFileSaveSim.Sensitive = isSensitive;
					TbSaveSim.Sensitive = isSensitive;
				}
				else
				{
					MenuFileSaveSim.Sensitive = false;
					TbSaveSim.Sensitive = false;
				}
			}
			else
			{
				// clear frame label
				LblFile.Markup = "<b>" + GlobalObjUI.LMan.GetString("framefile") + "</b>";
				MenuFileSaveSim.Sensitive = isSensitive;
				TbSaveSim.Sensitive = isSensitive;
			}
		}


		/// <summary>
		/// Set gui widgets before sim scan
		/// </summary>
		private void ScanSimBefore()
		{
			// Setup ProgressBar
			PBar.Fraction = 0;
			PBar.Adjustment.Lower=0;
			PBar.Adjustment.Upper=GlobalObjUI.SimADNRecordCount;
			PBar.Adjustment.Value=0;
			PBar.Visible=true;
			MainMenu.Sensitive = false;
			TopToolBar.Sensitive = false;
			FrameSim.Sensitive = false;
			FrameFile.Sensitive = false;
			MainClass.GtkWait();
		}


		/// <summary>
		/// Set gui widgets after sim scan
		/// </summary>
		private void ScanSimAfter()
		{
			PBar.Visible = false;
			MainMenu.Sensitive = true;
			TopToolBar.Sensitive = true;
			FrameSim.Sensitive = true;
			FrameFile.Sensitive = true;
			MainClass.GtkWait();
		}


		/// <summary>
		/// Choose file to save contacts.
		/// </summary>
		private string ChooseFileToSave(string dialogTitle)
		{
			string fileToSave = "";

			// New dialog to save sim contacts on file
			Gtk.FileChooserDialog FileBox = new Gtk.FileChooserDialog(dialogTitle,
											MainWindow,
											FileChooserAction.Save,
											GlobalObjUI.LMan.GetString("cancellbl"), Gtk.ResponseType.Cancel,
											GlobalObjUI.LMan.GetString("savelbl"), Gtk.ResponseType.Accept);

			// Filter for using only monosim files
			Gtk.FileFilter myFilter = new Gtk.FileFilter();
			myFilter.AddPattern("*.monosim");
			myFilter.Name = "monosim files";
			FileBox.AddFilter(myFilter);

			// Manage result of dialog box
			FileBox.Icon = Gdk.Pixbuf.LoadFromResource("monosim.png");
			int retFileBox = FileBox.Run();
			if ((ResponseType)retFileBox == Gtk.ResponseType.Accept)
			{
				// path of a right file returned
				fileToSave = FileBox.Filename;

				string chkfile = fileToSave.PadLeft(9).ToLower();
				if (chkfile.Substring(chkfile.Length-8) != ".monosim")
				{
					fileToSave += ".monosim";
				}

				FileBox.Destroy();
				FileBox.Dispose();
			}
			else
			{
				// nothing returned
				FileBox.Destroy();
				FileBox.Dispose();
				return "";
			}

			return fileToSave;
		}


		/// <summary>
		/// Write contacts on file
		/// </summary>
		private void WriteContactsOnFile(string filePath, List<Contact> contacts)
		{

			try
			{
				// save contacts
				StreamWriter sw = new StreamWriter(filePath,false);

				foreach(Contact cnt in contacts)
				{
					sw.WriteLine(cnt.Description);
					sw.WriteLine(cnt.PhoneNumber);
				}

				sw.Close();
				sw.Dispose();
				sw = null;

			}
			catch (Exception Ex)
			{
				log.Error("MainWindowClass::WriteContactsOnFile: " + Ex.Message + "\r\n" + Ex.StackTrace);
				MainClass.ShowMessage(MainWindow, "ERROR", Ex.Message, MessageType.Error);
				return;
			}

			MainClass.ShowMessage(MainWindow, "INFO", GlobalObjUI.LMan.GetString("filesaved"),MessageType.Info);
		}


		/// <summary>
		/// Write passed contacts on sim card (append or override)
		/// </summary>
		private void WriteContactsOnSim(Contacts contacts, bool isAppend)
		{
			// check for space on sim
			if (!isAppend && (contacts.SimContacts.Count > GlobalObjUI.SimADNRecordCount))
			{
				// No enough space on sim
				MainClass.ShowMessage(MainWindow, "ERROR",
					GlobalObjUI.LMan.GetString("nosimspace"), MessageType.Error);
				return;
			}

			if (isAppend && (contacts.SimContacts.Count > (GlobalObjUI.SimADNRecordCount -
														   GlobalObjUI.SimADNRecordNotEmpty)))
			{
				// No enough space on sim
				MainClass.ShowMessage(MainWindow, "ERROR",
					GlobalObjUI.LMan.GetString("nosimspace"), MessageType.Error);
				return;
			}

			SimUpdate(contacts, isAppend);
		}


		#endregion Private methods
	}
}
