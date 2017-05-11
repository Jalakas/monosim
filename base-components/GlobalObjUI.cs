using System;
using System.IO;
using System.Xml;
using System.Text;
using System.Collections.Generic;
using System.Reflection;

using comexbase;
using log4net;

namespace monosimbase
{
	public static partial class GlobalObjUI
	{
		// Attributes
		// Log4Net object
		private static readonly ILog log = LogManager.GetLogger(typeof(GlobalObjUI));
		private static LanguageManager lMan = null;
		private static int lenAlpha2 = 0;
		private static int tonNpiNumber = 0;
		private static string alphaID = "";
		private static string dialNum = "";
		// private static string configId = "";
		// private static string ext1Rec = "";
		private static string languageFolder = "";
		private static string languageTag = "";
		private static string numLength = "";
		private static string retStr = "";
		private static string tonNpi = "";

		#region Properties

		// Return language manager object
		public static LanguageManager LMan { get { return lMan; } }
		public static Contacts FileContacts {get; set;}
		public static Contacts SimContacts {get; set;}
		public static List<int> SimADNRecordEmptyID {get; set;}
		// Application folder path
		public static string AppPath {get; set;}
		public static string ContactsFilePath {get; set;}
		public static string SimADNError {get; set;}
		public static string SimICCID {get; set;}
		public static bool SimPin1Status {get; set;}
		public static int SimPin1Attempts {get; set;}
		public static int SimADNFileLen {get; set;}
		public static int SimADNMaxAlphaChars {get; set;}
		public static int SimADNPosition {get; set;}
		public static int SimADNRecordCount {get; set;}
		public static int SimADNRecordLen {get; set;}
		public static int SimADNRecordNoEmpty {get; set;}
		public static int SimADNStatus {get; set;}
		public static int SimADNVersion {get; set;}

		#endregion Properties
		#region Public Methods

		/// <summary>
		/// Set language to use
		/// </summary>
		public static void SetLanguage(string folderName)
		{
			// set application folder path
			string dllPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
			AppPath = new System.IO.FileInfo(dllPath).DirectoryName;

			string envLang = System.Globalization.CultureInfo.CurrentCulture.IetfLanguageTag;
			languageFolder = AppPath + Path.DirectorySeparatorChar + "Languages";

			// check for language folder
			if (!Directory.Exists(languageFolder))
			{
				// use share folder to search languages
				languageFolder = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData) +
								 Path.DirectorySeparatorChar + folderName +
								 Path.DirectorySeparatorChar + "Languages";

				if (!Directory.Exists(languageFolder))
				{
					// no languages founded
					throw new Exception("no language folder founded... ");
				}
			}

			// check for language file
			DirectoryInfo di = new DirectoryInfo(languageFolder);
			if (di.GetFiles("monosim-" + envLang + ".xml").Length == 1)
			{
				// language file exists, use it
				languageTag = envLang;
			}
			else
			{
				// language file don't exists, use en-US as default
				languageTag = "en-US";
			}

			log.Debug("GlobalObjUI::SetLanguage: LanguagePath=" + languageFolder + Path.DirectorySeparatorChar + "monosim-" + languageTag + ".xml");
			lMan = new LanguageManager(languageFolder + Path.DirectorySeparatorChar + "monosim-" + languageTag + ".xml");

			return;
		}


		/// <summary>
		/// Extract contacts from ADN records
		/// </summary>
		public static string FromRecordsToContacts()
		{
			for (int r=0; r<ADNrecords.Count; r++)
			{
				// try to extract contact info
				retStr = DecodeSimADNRecord(ADNrecords[r]);

				if (retStr != "")
				{
					// error detected
					return retStr;
				}
			}

			return "";
		}


		/// <summary>
		/// Check for contacts with description chars len > MaxAlphaCharsLen
		/// </summary>
		public static string CheckAlphaCharsLen(Contacts contacts)
		{
			// loop for each contact
			foreach (Contact contact in contacts.SimContacts)
			{
				// check for contact description length
				if (contact.Description.Length > SimADNMaxAlphaChars)
				{
					return contact.Description;
				}
			}

			return "";
		}


		#endregion Public Methods
		#region Private Methods


		/// <summary>
		/// Decode record to obtain contact infos
		/// </summary>
		private static string DecodeSimADNRecord(string recordValue)
		{
			try
			{
				lenAlpha2	= (recordValue.Length - 28);

				alphaID		= recordValue.Substring(0, 				lenAlpha2);
				numLength	= recordValue.Substring(lenAlpha2,		2);
				tonNpi		= recordValue.Substring(lenAlpha2 + 2,	2);
				dialNum		= recordValue.Substring(lenAlpha2 + 4,	20);
				// configId	= recordValue.Substring(lenAlpha2 + 24,	2);
				// ext1Rec	= recordValue.Substring(lenAlpha2 + 26,	2);

				if (alphaID.Substring(0, 2) == "80") alphaID = HexToUnicode(alphaID);
				else alphaID = HexToASCII(alphaID);

				dialNum = SwapNumber(dialNum, (Convert.ToInt32(numLength, 16)-1) * 2);

				tonNpiNumber = Convert.ToInt32(tonNpi, 16);

				if ((tonNpiNumber&16) > 0)
				{
					// international
					dialNum = "+" + dialNum;
				}

				log.Debug("GlobalObjUI::DecodeSimADNRecord: Contact = " + alphaID + " - " + dialNum);

			}
			catch (Exception Ex)
			{
				log.Error("GlobalObjUI::DecodeSimADNRecord: " + Ex.Message + "\r\n" + Ex.StackTrace);
				return Ex.Message;
			}

			// add phone number and description for new founded contact
			SimContacts.SimContacts.Add(new Contact(alphaID, dialNum));
			return "";
		}


		/// <summary>
		/// Get ASCII string from hexadecimal string
		/// </summary>
		private static string HexToASCII(string hexValue)
		{
			List<byte> bArray = new List<byte>();
			string curHexValue = "";

			for (int b=0; b < hexValue.Length; b+=2)
			{
				curHexValue = hexValue.Substring(b, 2);

				if (Convert.ToInt32(curHexValue, 16) < 32)
				{
					log.Debug("GlobalObjUI::HexToASCII: Found 0x" + curHexValue + ", ignoring non symbol character");
					continue;
				}
				else if (curHexValue == "EF" || curHexValue == "BF" || curHexValue == "BD")
				{
					log.Debug("GlobalObjUI::HexToASCII: Found 0x" + curHexValue + ", some symbols are probably lost");
					continue;
				}
				else if (curHexValue == "81")
				{
					log.Debug("GlobalObjUI::HexToASCII: Found 0x" + curHexValue + ", ignoring 3 bytes from beginning");
					b+=4;
					continue;
				}
				else if (curHexValue != "FF")		// check for no empty byte
				{
					bArray.Add(byte.Parse(curHexValue, System.Globalization.NumberStyles.AllowHexSpecifier));
				}
			}

			Encoding enc = new UTF8Encoding(false, false);
			return enc.GetString(bArray.ToArray());
		}


		/// <summary>
		/// Get Unicode string from hexadecimal string
		/// </summary>
		private static string HexToUnicode(string hexValue)
		{
			List<byte> bArray = new List<byte>();
			string curHexValue = "";

			for (int b=0; b < hexValue.Length; b+=2)
			{
				curHexValue = hexValue.Substring(b, 2);

				if (curHexValue == "80")		// check for unicode
				{
					log.Debug("GlobalObjUI::HexToUnicode: Found 0x" + curHexValue + ", already using Unicode for conversion");
					continue;
				}
				else if (curHexValue != "FF")	// check for no empty byte
				{
					bArray.Add(byte.Parse(curHexValue, System.Globalization.NumberStyles.AllowHexSpecifier));
				}
			}

			Encoding enc = new UnicodeEncoding(true, false);
			return enc.GetString(bArray.ToArray());
		}


		/// <summary>
		/// Get hexadecimal value from ASCII string
		/// </summary>
		private static string HexFromAscii(string asciiValue)
		{
			string hexOut = "";
			byte[] inBytes = UTF8Encoding.UTF8.GetBytes(asciiValue);

			for (int j=0; j<inBytes.Length; j++)
			{
				hexOut += inBytes[j].ToString("X2");
			}

			return hexOut;
		}


		/// <summary>
		/// Swap phone number digits
		/// </summary>
		private static string SwapNumber(string inNumber, int numDigits)
		{
			string outNumber = "";

			// loop for each byte
			for (int k=0; k<numDigits; k +=2)
			{
				// swap digits in byte
				outNumber += inNumber.Substring(k+1,1) + inNumber.Substring(k,1);
			}

			outNumber = outNumber.Replace("A", "*");
			outNumber = outNumber.Replace("B", "#");
			outNumber = outNumber.Replace("F", "");

			return outNumber;
		}


		/// <summary>
		/// Prepare record to store contact on sim
		/// </summary>
		private static string PrepareRecord(Contact cnt, out string record)
		{
			string outNumber = "";
			int lenNumber = 0;
			string inNumber = cnt.PhoneNumber;
			string digit = "";
			string tonNpi = "81";
			record = HexFromAscii(cnt.Description);
			record = record.PadRight(SimADNMaxAlphaChars*2, 'F');

			int tmpInt;

			if (inNumber.Substring(0,1) == "+")
			{
				// international number
				tonNpi = "91";
				inNumber = inNumber.Substring(1);
			}

			// check for phone number digits
			for(int p=0; p<inNumber.Length; p++)
			{
				digit = inNumber.Substring(p, 1);
				if (!int.TryParse(digit, out tmpInt) && digit != "*" && digit != "#")
				{
					log.Debug("GlobalObjUI::PrepareRecord: WRONG NUMBER " + cnt.PhoneNumber);
					return "WRONG NUMBER " + cnt.PhoneNumber;
				}
			}

			// Prepare Phone number
			if (inNumber.Length%2 != 0)
			{
				// add digit
				inNumber += "F";
			}

			// len of numner + tonnpi byte
			lenNumber = (inNumber.Length/2) + 1;

			// loop for each byte
			for (int k=0; k<inNumber.Length; k +=2)
			{
				// swap digits in byte
				outNumber += inNumber.Substring(k+1,1) + inNumber.Substring(k,1);
			}

			outNumber = outNumber.Replace("*", "A");
			outNumber = outNumber.Replace("#", "B");
			outNumber = outNumber.PadRight(20, 'F');

			record += lenNumber.ToString("X2");
			record += tonNpi;
			record += outNumber;
			record += "FFFF";

			return "";
		}


		#endregion Private Methods


	}
}
