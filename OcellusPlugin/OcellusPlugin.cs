﻿using System;
using System.Collections.Generic;
using System.Net;
using System.IO;
using System.Windows.Forms;
using System.Web.Script.Serialization;


// *****************************
// *  Voice Attack functions   *
// *****************************

namespace OcellusPlugin
{   
    public class OcellusPlugin
    {
        public static string VA_DisplayName()
        {
            return "Ocellus - Elite: Dangerous Assistant " + Upgrade.pluginVersion;  
        }

        public static string VA_DisplayInfo()
        {
            return "VoiceAttack Plugin\r\n\r\nFor use with Elite: Dangerous\r\n\r";  
        } 

        public static Guid VA_Id()
        {

            return new Guid("{A818A69A-D6A9-4CC8-943C-E6ABBAF4C76C}");  
        }

        public static void VA_Init1(ref Dictionary<string, object> state, ref Dictionary<string, Int16?> shortIntValues, ref Dictionary<string, string> textValues, ref Dictionary<string, int?> intValues, ref Dictionary<string, decimal?> decimalValues, ref Dictionary<string, Boolean?> booleanValues, ref Dictionary<string, object> extendedValues)
        {
            // Setup plugin storage directory - used for cookies and debug logs
            string appPath = Config.Path();
            string cookieFile = Config.CookiePath();
            string debugFile = Path.Combine(appPath, "debug.log");

            // Determine Elite Dangerous directories
            string gamePath = Elite.getGamePath();
            string logPath = Elite.getGameLogPath(gamePath);
            string gameStartString = PluginRegistry.getStringValue("startPath");
            string gameStartParams = PluginRegistry.getStringValue("startParams");
            state.Add("VAEDgamePath", gamePath);
            state.Add("VAEDlogPath", logPath);
            textValues["VAEDgameStartString"] = gameStartString;
            textValues["VAEDgameStartParams"] = gameStartParams;
            state.Add("VAEDnetLogFile", String.Empty);


            // verboseEnabled:  0 - not enabled, 1 - was already enabled, 2 - just enabled now
            string gameConfigPath = Path.Combine(gamePath, "Products", "elite-dangerous-64");
            int verboseEnabled = Elite.enableVerboseLogging(gameConfigPath);
            state.Add("VAEDverboseLoggingEnabled", verboseEnabled);


            // Determine if ED is currently running
            Int32 processId = Elite.getPID();
            state.Add("VAEDelitePid", processId);


            // Reset current location in netLog reading to the top
            long pos = 0;
            state.Add("VAEDcurrentLogPosition", pos);

            booleanValues["VAEDisEliteRunning"] = Elite.isEliteRunning();

            if (verboseEnabled == 2 && booleanValues["VAEDEliteRunning"] == true )
            {
                booleanValues["VAEDneedRestart"] = true;
            }
            else if (verboseEnabled == 0)
            {
                booleanValues["VAEDverboseProblem"] = true;
            }

            // Load EDDB Index into memory
            Eddb.loadEddbIndex(ref state);

            CookieContainer cookieJar = new CookieContainer();

            state.Add("VAEDloggedIn", "no");

            if (File.Exists(cookieFile))
            {
                // If we have cookies then we are likely already logged in
                cookieJar = Web.ReadCookiesFromDisk(cookieFile);
                state.Add("VAEDcookieContainer", cookieJar);
                state["VAEDloggedIn"] = "ok";
            }
        }

        public static void VA_Exit1(ref Dictionary<string, object> state)
        {
            //this function gets called when VoiceAttack is closing (normally).  You would put your cleanup code in here, but be aware that your code must be robust enough to not absolutely depend on this function being called

        }

        public static void VA_Invoke1(String context, ref Dictionary<string, object> state, ref Dictionary<string, Int16?> shortIntValues, ref Dictionary<string, string> textValues, ref Dictionary<string, int?> intValues, ref Dictionary<string, decimal?> decimalValues, ref Dictionary<string, Boolean?> booleanValues, ref Dictionary<string, object> extendedValues)
        {
            Debug.Write("COMMAND:  " + context);
            switch (context)
            {

                case "check upgrade":
                    if (Upgrade.needUpgrade())
                    {
                        booleanValues["VAEDneedUpgrade"] = true;
                    }
                    else
                    {
                        booleanValues["VAEDneedUpgrade"] = false;
                    }
                    break;
                case "clear debug":
                    Debug.Clear();
                    break;
                case "get debug":
                    string tempDebug = Debug.Path();
                    textValues["VAEDdebugFile"] = tempDebug;
                    break;
                case "coriolis":
                    Coriolis.createCoriolisJson(ref state);
                    break;
                case "edit web variables":
                    var webVarsForm = new WebVars.EditWebVars();
                    webVarsForm.ShowDialog();
                    break;
                case "edit file variables":
                    textValues["VAEDvalue"] = FileVar.getFileVarFilename();
                    break;
                case "update web vars":
                    //WebVar.readWebVars(ref state, ref textValues, ref intValues, ref booleanValues);
                    break;
                case "update file vars":
                    FileVar.readFileVars(ref state, ref textValues, ref intValues, ref booleanValues);
                    break;
                case "set credentials":
                    var credentialsForm = new Credentials.Login();
                    credentialsForm.ShowDialog();
                    break;
                case "credentials":
                    string email = PluginRegistry.getStringValue("email");
                    string password = PluginRegistry.getStringValue("password");
                    booleanValues["VAEDstatusCredentials"] = false;
                    if (email != null && email != string.Empty && password != null && password != string.Empty)
                    {
                        booleanValues["VAEDstatusCredentials"] = true;
                    }
                    break;
                case "clipboard":
                    if (Clipboard.ContainsText(TextDataFormat.Text))
                    {
                        textValues.Add("VAEDclipboard", Clipboard.GetText());
                    }
                    break;

                case "authenticate":
                    CookieContainer cookieContainer = new CookieContainer();

                    Tuple<CookieContainer, string> tAuthentication = Companion.loginToAPI();

                    cookieContainer = tAuthentication.Item1;
                    string loginResponse = tAuthentication.Item2;
                    Debug.Write("loginResponse:  " + loginResponse);
                    state["VAEDloggedIn"] = loginResponse;
                    textValues["VAEDauthenticationStatus"] = loginResponse;
                    if (loginResponse == "verification" || loginResponse == "ok")
                    {
                        if (state.ContainsKey("VAEDcookieContainer"))
                        {
                            state["VAEDcookieContainer"] = cookieContainer;
                        }
                        else
                        {
                            state.Add("VAEDcookieContainer", cookieContainer);
                        }
                    }
                    else
                    {
                        state["VAEDloggedIn"] = "no";
                    }
                    break;

                case "verification":
                    if (state["VAEDloggedIn"].ToString() == "verification")
                    {
                        if (textValues.ContainsKey("VAEDvalue"))
                        {
                            CookieContainer verifyCookies = (CookieContainer)state["VAEDcookieContainer"];

                            Tuple<CookieContainer, string> tVerify = Companion.verifyWithAPI(verifyCookies, textValues["VAEDvalue"]);
                            verifyCookies = tVerify.Item1;
                            string verifyResponse = tVerify.Item2;
                            state["VAEDloggedIn"] = verifyResponse;
                            state["VAEDcookieContainer"] = verifyCookies;
                            textValues["VAEDverificationStatus"] = verifyResponse;
                            if (verifyResponse == "ok")
                            {
                                Web.WriteCookiesToDisk(Config.CookiePath(), verifyCookies);
                            }
                            break;
                        }
                    }
                    else if (state["VAEDloggedIn"].ToString() == "no")
                    {
                        Debug.Write("Not logged in - can't verify");
                        textValues.Add("VAEDprofileStatus", "no");
                        break;
                    }
                    Debug.Write("Error:  Unknown verification problem");
                    break;

                case "update eddn":
                    break;

                case "profile":
                    if (state["VAEDloggedIn"].ToString() == "ok" && state.ContainsKey("VAEDcookieContainer"))
                    {
                        int profileCooldown = Utilities.isCoolingDown(ref state, "VAEDprofileCooldown");
                        if (profileCooldown > 0)
                        {
                            Debug.Write("Get Profile is cooling down: " + profileCooldown.ToString() + " seconds remain.");
                            break;
                        }

                        textValues["VAEDprofileStatus"] = "ok";

                        CookieContainer profileCookies = (CookieContainer)state["VAEDcookieContainer"];

                        Tuple<CookieContainer, string> tRespon = Companion.getProfile(profileCookies);

                        state["VAEDcookieContainer"] = tRespon.Item1;
                        Web.WriteCookiesToDisk(Config.CookiePath(), tRespon.Item1);
                        string htmlData = tRespon.Item2;

                        JavaScriptSerializer serializer = new JavaScriptSerializer();

                            
                        string currentSystem = "";
                        string currentStarport = "";
                        Boolean currentlyDocked = false;
                        var result = new Dictionary<string, dynamic>();
                        try
                        {
                            result = serializer.Deserialize<Dictionary<string, dynamic>>(htmlData);
                            state["VAEDcompanionDict"] = result;
                            string cmdr = result["commander"]["name"];
                            int credits = result["commander"]["credits"];
                            int debt = result["commander"]["debt"];
                            int shipId = result["commander"]["currentShipId"];
                            string currentShipId = shipId.ToString();
                            currentlyDocked = result["commander"]["docked"];
                            string combatRank = Elite.combatRankToString(result["commander"]["rank"]["combat"]);
                            string tradeRank = Elite.tradeRankToString(result["commander"]["rank"]["trade"]);
                            string exploreRank = Elite.exploreRankToString(result["commander"]["rank"]["explore"]);
                            string cqcRank = Elite.cqcRankToString(result["commander"]["rank"]["cqc"]);

                            string federationRank = Elite.federationRankToString(result["commander"]["rank"]["federation"]);
                            string empireRank = Elite.empireRankToString(result["commander"]["rank"]["empire"]);

                            string powerPlayRank = Elite.powerPlayRankToString(result["commander"]["rank"]["power"]);

                            Dictionary<string, object> allShips = result["ships"];
                            int howManyShips = allShips.Count;
                            int cargoCapacity = result["ship"]["cargo"]["capacity"];
                            int quantityInCargo = result["ship"]["cargo"]["qty"];
                            string currentShip = result["ships"][currentShipId]["name"];
                            result["commander"]["currentShip"] = currentShip;

                            List<string> keys = new List<string>(allShips.Keys);

                            // Null out ship locations
                            string[] listOfShips = Elite.listofShipsShortNames();
                            foreach (string ship in listOfShips)
                            {
                                textValues["VAEDship-" + ship + "-1"] = null;
                                intValues["VAEDshipCounter-" + ship] = 0;
                            }

                            foreach (string key in keys)
                            {

                                string tempShip = result["ships"][key]["name"];

                                string tempSystem = null;
                                if (result["ships"][key].ContainsKey("starsystem"))
                                {
                                    tempSystem = result["ships"][key]["starsystem"]["name"];
                                    if (key == currentShipId)
                                    {
                                        currentSystem = tempSystem;
                                    }
                                }
                                string tempStation = null;
                                if (result["ships"][key].ContainsKey("station"))
                                {
                                    tempStation = result["ships"][key]["station"]["name"];
                                    if (key == currentShipId)
                                    {
                                        currentStarport = tempStation;
                                    }
                                }

                                string shipCounterString = "VAEDshipCounter-" + tempShip;
                                intValues[shipCounterString]++;
                                int counterInt = (int)intValues[shipCounterString];
                                string counterStr = counterInt.ToString();
                                textValues["VAEDship-" + tempShip + "-" + counterStr] = tempSystem;
                            }

                            //Setup ambiguous ship variables
                            textValues["VAEDambiguousViper"] = null;
                            textValues["VAEDambiguousCobra"] = null;
                            textValues["VAEDambiguousDiamondback"] = null;
                            textValues["VAEDambiguousAsp"] = null;
                            textValues["VAEDambiguousEagle"] = null;
                                
                            if ((intValues["VAEDshipCounter-Viper"] + intValues["VAEDshipCounter-Viper_MkIV"]) == 1)
                            {
                                if (textValues["VAEDship-Viper-1"] != null)
                                {
                                    textValues["VAEDambiguousViper"] = textValues["VAEDship-Viper-1"];
                                }
                                else
                                {
                                    textValues["VAEDambiguousViper"] = textValues["VAEDship-Viper_MkIV-1"];
                                }
                                    
                            }
                            if ((intValues["VAEDshipCounter-CobraMkIII"] + intValues["VAEDshipCounter-CobraMkIV"]) == 1)
                            {
                                if (textValues["VAEDship-CobraMkIII-1"] != null)
                                {
                                    textValues["VAEDambiguousCobra"] = textValues["VAEDship-CobraMkIII-1"];
                                }
                                else
                                {
                                    textValues["VAEDambiguousCobra"] = textValues["VAEDship-CobraMkIV-1"];
                                }
                            }
                            if ((intValues["VAEDshipCounter-DiamondBack"] + intValues["VAEDshipCounter-DiamondBackXL"]) == 1)
                            {
                                if (textValues["VAEDship-DiamondBack-1"] != null)
                                {
                                    textValues["VAEDambiguousDiamondBack"] = textValues["VAEDship-DiamondBack-1"];
                                }
                                else
                                {
                                    textValues["VAEDambiguousDiamondBack"] = textValues["VAEDship-DiamondBackXL-1"];
                                }
                            }
                            if ((intValues["VAEDshipCounter-Asp"] + intValues["VAEDshipCounter-Asp_Scout"]) == 1)
                            {
                                if (textValues["VAEDship-Asp-1"] != null)
                                {
                                    textValues["VAEDambiguousAsp"] = textValues["VAEDship-Asp-1"];
                                }
                                else
                                {
                                    textValues["VAEDambiguousAsp"] = textValues["VAEDship-Asp_Scout-1"];
                                }
                            }
                            if ((intValues["VAEDshipCounter-Eagle"] + intValues["VAEDshipCounter-Empire_Eagle"]) == 1)
                            {
                                if (textValues["VAEDship-Eagle-1"] != null)
                                {
                                    textValues["VAEDambiguousEagle"] = textValues["VAEDship-Eagle-1"];
                                }
                                else
                                {
                                    textValues["VAEDambiguousEagle"] = textValues["VAEDship-Empire_Eagle-1"];
                                }
                            }

                            intValues["VAEDnumberOfShips"] = howManyShips;
                            textValues["VAEDcmdr"] = cmdr;
                            intValues["VAEDcredits"] = credits;
                            intValues["VAEDloan"] = debt;
                            booleanValues["VAEDcurrentlyDocked"] = currentlyDocked;
                            textValues["VAEDcombatRank"] = combatRank;
                            textValues["VAEDexploreRank"] = exploreRank;
                            textValues["VAEDtradeRank"] = tradeRank;
                            textValues["VAEDcqcRank"] = cqcRank;
                            textValues["VAEDfederationRank"] = federationRank;
                            textValues["VAEDempireRank"] = empireRank;
                            textValues["VAEDcurrentShip"] = currentShip;
                            intValues["VAEDcargoCapacity"] = cargoCapacity;
                            intValues["VAEDquantityInCargo"] = quantityInCargo;
                        }
                        catch (Exception ex)
                        {
                            Debug.Write("Error: Unable to parse Companion API output " + ex.ToString());
                            Debug.Write(htmlData);
                            textValues["VAEDprofileStatus"] = "error";
                        }

                        textValues["VAEDeddbStarportId"] = null;
                        textValues["VAEDcurrentStarport"] = null;
                        textValues["VAEDeddbSystemId"] = null;
                        textValues["VAEDcurrentSystem"] = null;

                        if (currentlyDocked)
                        {
                            textValues["VAEDcurrentSystem"] = currentSystem;
                            textValues["VAEDcurrentStarport"] = currentStarport;

                            //Set Station Services
                            booleanValues["VAEDstarportCommodities"] = false;
                            booleanValues["VAEDstarportShipyard"] = false;
                            booleanValues["VAEDstarportOutfitting"] = false;
                            if (result["lastStarport"].ContainsKey("commodities"))
                            {
                                booleanValues["VAEDstarportCommodities"] = true;
                            }
                            if (result["lastStarport"].ContainsKey("ships") && result["lastStarport"]["ships"].ContainsKey("shipyard_list"))
                            {
                                booleanValues["VAEDstarportShipyard"] = true;
                            }
                            if (result["lastStarport"].ContainsKey("ships") && result["lastStarport"].ContainsKey("modules"))
                            {
                                booleanValues["VAEDstarportOutfitting"] = true;
                            }
                        }
                        else
                        {
                            // If not docked, then we get the System information from the netLog file
                            long currentPosition = (long)state["VAEDcurrentLogPosition"];
                            Int32 elitePid = (Int32)state["VAEDelitePid"];

                            Tuple<Boolean, string, string, long, Int32> tLogReturn = Elite.tailNetLog(state["VAEDlogPath"].ToString(), state["VAEDnetLogFile"].ToString(), currentPosition, elitePid);

                            Boolean success = tLogReturn.Item1;
                            currentSystem = tLogReturn.Item2.ToString();
                            state["VAEDnetLogFile"] = tLogReturn.Item3.ToString();
                            state["VAEDcurrentLogPosition"] = tLogReturn.Item4;
                            state["VAEDelitePid"] = tLogReturn.Item5;

                            if (success)
                            {
                                textValues["VAEDcurrentSystem"] = currentSystem;
                            }
                        }

                        if (state.ContainsKey("VAEDeddbIndex"))
                        {
                            Dictionary<string, dynamic> tempEddb = (Dictionary<string, dynamic>)state["VAEDeddbIndex"];

                            if (textValues["VAEDcurrentSystem"] != null && tempEddb.ContainsKey(textValues["VAEDcurrentSystem"]))
                            {
                                textValues["VAEDeddbSystemId"] = tempEddb[textValues["VAEDcurrentSystem"]]["id"].ToString();
                                if (textValues["VAEDcurrentStarport"] != null && tempEddb[textValues["VAEDcurrentSystem"]]["stations"].ContainsKey(textValues["VAEDcurrentStarport"]))
                                {
                                    textValues["VAEDeddbStarportId"] = tempEddb[textValues["VAEDcurrentSystem"]]["stations"][textValues["VAEDcurrentStarport"]].ToString();
                                }
                            }

                        }
                        if (1 == 1) // Debug
                        {
                            // Write out JSON
                            Debug.Write("----------------HTMLDATA FOLLOWS------------------------------");
                            Debug.Write(htmlData);

                            Debug.Write("TEXT VALUES");
                            foreach (string key in textValues.Keys)
                            {
                                if (textValues[key] != null)
                                {
                                    Debug.Write(key + ":  " + textValues[key]);
                                }

                            }

                            Debug.Write("INTEGER VALUES");
                            foreach (string key in intValues.Keys)
                            {
                                if (intValues[key] != null)
                                {
                                    Debug.Write(key + ":  " + intValues[key].ToString());
                                }
                            }

                            Debug.Write("BOOLEAN VALUES");
                            foreach (string key in booleanValues.Keys)
                            {
                                if (booleanValues[key] != null)
                                {
                                    Debug.Write(key + ":  " + booleanValues[key].ToString());
                                }
                            }
                        }
                    }
                    else // Not logged in
                    {
                        textValues["VAEDprofileStatus"] = "credentials";
                    }
                    break;

                default:
                    Debug.Write("Error: unknown command");
                    break;
                
            }          
        }
    }
}