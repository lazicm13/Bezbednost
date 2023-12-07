﻿using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.ServiceModel;
using System.Text;
using Common;
using Common.Manager;
using Common.Models;
using Manager;
using SymmetricAlgorithms;
using System.Diagnostics;
using System.IO;
using System.Runtime.Serialization;

namespace Client
{
    class Program
    {
        

        static void Main(string[] args)
        {
            Console.WriteLine($"Logovani korisnik { WindowsIdentity.GetCurrent().Name}");
            while (true)
            {
                int operacija = Meni();
                if(operacija == 3 || operacija == 4)
                {
                    CertConnection(operacija);
                }
                else
                {
                    AuthConnection(operacija);
                }
            }
        }


        public static int Meni()
        {
            Console.WriteLine("Izaberite zahtev: ");
            Console.WriteLine("\t1 - Kreiranje naloga");
            Console.WriteLine("\t2 - Povlacenje sertifikata ");
            Console.WriteLine("\t3 - Izvrsenje transakcije");
            Console.WriteLine("\t4 - Reset pin koda");
            Console.WriteLine("\t0 - Izlaz");

            Console.Write("Unesite Vas izbor: ");
            int izbor = Int32.Parse(Console.ReadLine());
            return izbor;
        }

        static void CertConnection(int operacija)
        {
            string srvCertCN = "server";
            string secretKey = "123456";

            NetTcpBinding binding = new NetTcpBinding();
            binding.Security.Transport.ClientCredentialType = TcpClientCredentialType.Certificate;

            X509Certificate2 srvCert = CertManager.GetCertificateFromStorage(StoreName.TrustedPeople, StoreLocation.LocalMachine, srvCertCN);
            EndpointAddress address = new EndpointAddress(new Uri("net.tcp://localhost:4001/SertService"), new X509CertificateEndpointIdentity(srvCert));

            // digitalni potpisi
            string signCertCN = Common.Manager.Formatter.ParseName(WindowsIdentity.GetCurrent().Name) + "_ds";

            X509Certificate2 certificateSign = CertManager.GetCertificateFromStorage(StoreName.My, StoreLocation.LocalMachine, signCertCN);
            using (ClientCert proxy = new ClientCert(binding, address))
            {
                try
                {
                    if(operacija == 3)
                    { 
                        Console.WriteLine("Certificate communication is active");
                        Console.WriteLine("Izaberite akciju: ");
                        Console.WriteLine("\t1 - Uplata novca");
                        Console.WriteLine("\t2 - Podizanje novca ");
                        Console.WriteLine("\t0 - Izlaz");
                        Console.Write("Unesite Vas izbor: ");
                        int izbor = Int32.Parse(Console.ReadLine());

                        if (izbor == 0) return;
                        Console.Write("Unesite broj racuna na kom ce se izvrsiti transakcija: ");
                        string brojRacuna = Console.ReadLine();
                        double svotaNovca = 0;
                        byte[] brojRacunaEncrypted = EncryptString(brojRacuna, secretKey);
                        byte[] svotaNovcaEnc = EncryptDouble(svotaNovca, secretKey);
                        byte[] signature = DigitalSignature.Create(izbor.ToString(), Manager.HashAlgorithm.SHA1, certificateSign);

                        if (izbor == 1)
                        {
                            Console.Write("Unesite koliko novca zelite da uplatite: ");
                            svotaNovca = Double.Parse(Console.ReadLine());
                            Console.WriteLine("PRE");
                            if (proxy.IzvrsiTransakciju(izbor, brojRacunaEncrypted, svotaNovcaEnc, signature)) Console.WriteLine($"Uspesno ste uplatili {svotaNovca} dinara.");
                            else Console.WriteLine("Transakcija nije moguca! Uneli ste nepostojeci broj racuna!");
                            Console.WriteLine("POSLE");
                        }
                        else if (izbor == 2)
                        {
                            Console.Write("Unesite koliko novca zelite da podignete: ");
                            svotaNovca = Double.Parse(Console.ReadLine());
                            if (proxy.IzvrsiTransakciju(izbor, brojRacunaEncrypted, svotaNovcaEnc, signature)) Console.WriteLine($"Uspesno ste podigli {svotaNovca} dinara.");
                            else Console.WriteLine("Transakcija nije moguca! Uneli ste nepostojeci broj racuna ili nemate dovoljno sredstava na racunu!");
                        }

                    }else if(operacija == 4)
                    {
                        Console.Write("Unesite broj naloga: ");
                        string brojNaloga = Console.ReadLine();
                        string pin = ResetPin(brojNaloga);
                        byte[] signature = DigitalSignature.Create(brojNaloga, Manager.HashAlgorithm.SHA1, certificateSign);
                        if (proxy.ResetujPinKod(pin, brojNaloga, signature))
                        {
                            Console.WriteLine("Uspesna promena pin koda!");
                        }
                        else
                        {
                            Console.WriteLine("Greska! Pin kod nije promenjen!");
                        }
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message );
                }
            }
        }

        static void AuthConnection(int broj)
        {
            NetTcpBinding binding = new NetTcpBinding();
            string address = "net.tcp://localhost:4000/WinService";

            string secretKey = "123456";

            binding.Security.Mode = SecurityMode.Transport;
            binding.Security.Transport.ClientCredentialType = TcpClientCredentialType.Windows;
            binding.Security.Transport.ProtectionLevel = System.Net.Security.ProtectionLevel.EncryptAndSign;

            // digitalni potpisi
            string signCertCN = Common.Manager.Formatter.ParseName(WindowsIdentity.GetCurrent().Name) + "_ds";
            X509Certificate2 certificateSign = CertManager.GetCertificateFromStorage(StoreName.My, StoreLocation.LocalMachine, signCertCN);

            using (ClientWin proxy = new ClientWin(binding, address))
            {
                Console.WriteLine("Windows Authentication communication is active");
                // Ucitavanje korisnika i naloga u inMemory bazu
                ReadAccounts(proxy);
                //ReadUsers(proxy);
                try
                {
                    switch (broj)
                    {
                        case 1:
                            Account acc = KreirajNalog();
                            byte[] signature = null;
                            //byte[] signature = DigitalSignature.Create(acc.BrojRacuna, Manager.HashAlgorithm.SHA1, certificateSign);
                            byte[] account = CreateEncryptedAccount(acc, secretKey);
                            if (proxy.KreirajNalog(account, signature))
                            {
                                Console.WriteLine("Cestitamo! Uspesno ste kreirali nalog!");
                            }
                            else
                            {
                                Console.WriteLine("Greska! Nalog sa unetim brojem vec postoji!");
                            }
                            break;
                        case 2:
                            break;

                        case 5:
                            break;

                        case 0:
                            break;
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message + "\n" + e.StackTrace);
                    Console.ReadKey();
                }
            }
        }

        static string ReadPassword()
        {

            string password = "";
            ConsoleKeyInfo key;

            while (true)
            {
                key = Console.ReadKey(true);

                if (key.Key == ConsoleKey.Enter)
                {
                    Console.WriteLine();
                    break;
                }
                else if (key.Key == ConsoleKey.Backspace)
                {
                    if (password.Length > 0)
                    {
                        password = password.Remove(password.Length - 1);
                        Console.Write("\b \b"); // Erase the character from the console
                    }
                }
                else
                {
                    password += key.KeyChar;
                    Console.Write("*"); // Display asterisks instead of the actual characters
                }
            }

            return password;
        }
        public static Account KreirajNalog()
        {
            Console.Write("Unesite broj naloga: ");
            string broj = Console.ReadLine();
            Console.Write("Unesite PIN: ");
            string pin = ReadPassword();
            Console.Write("Potvrdite PIN: ");
            string pinPotvrda = ReadPassword();



            if (pin.Equals(pinPotvrda))
            {
                return new Account(broj, pin);
            }
            else
            {
                Console.WriteLine("Greska! Uneti PIN kodovi se ne poklapaju.");
                return null;
            }
        }

        public static string ResetPin(string brojNaloga)
        {
          //  if(IMDatabase.AllUserAccountsDB.ContainsKey(brojNaloga.Trim()))
       //     {
                Console.Write("Unesite stari Pin: ");
                string stariPin = ReadPassword();
                if (IMDatabase.AllUserAccountsDB[brojNaloga].Pin.Equals(stariPin))
                {
                    Console.Write("Unesite novi Pin: ");
                    string noviPin = ReadPassword();
                    Console.Write("Potvrdite novi Pin: ");
                    string noviPinPotvrda = ReadPassword();
                    if (noviPin.Equals(noviPinPotvrda))
                    {
                        Console.WriteLine("Pin kod je uspesno promenjen!");
                    }
                    else
                    {
                        Console.WriteLine("Greska! Pin kodovi se ne poklapaju!");
                        return null;
                    }

                    return noviPin;
                }
                else
                {
                    Console.WriteLine("Pogresan pin kod ili broj naloga. Pokusajte ponovo!");
                    return null;
                }
          //  }
        //    else
         //   {
          //      Console.WriteLine("Greska! Uneti broj naloga ne postoji!");
          //      return null;
         //   }
        }

        public static void ReadAccounts(ClientWin proxy)
        {
            Dictionary<string, Account> AllUsersDict = proxy.ReadDict();
            foreach (Account acc in AllUsersDict.Values)
            {
                if (!IMDatabase.AllUserAccountsDB.ContainsKey(acc.BrojRacuna))
                {
                    IMDatabase.AllUserAccountsDB.Add(acc.BrojRacuna, acc);
                }
            }
        }

        /*
        public static void ReadUsers(ClientWin proxy)
        {
            Dictionary<string, User> AllUsersDict = proxy.ReadDictUsers();
            foreach (User u in AllUsersDict.Values)
            {
                if (!IMDatabase.UsersDB.ContainsKey(u.Username))
                {
                    IMDatabase.UsersDB.Add(u.Username, u);
                }
            }
        }
        */

        public static byte[] SerializeAccount(Account account)
        {
            using (MemoryStream memoryStream = new MemoryStream())
            {
                DataContractSerializer serializer = new DataContractSerializer(typeof(Account));
                serializer.WriteObject(memoryStream, account);
                return memoryStream.ToArray();
            }
        }

        public static byte[] CreateEncryptedAccount(Account account, string secretKey)
        {
            byte[] serializedAccount = SerializeAccount(account);
            return TripleDES_Symm_Algorithm.Encrypt(serializedAccount, secretKey);
        }

        public static byte[] EncryptString(string message, string secretKey)
        {
            byte[] bytesToEncrypt = Encoding.UTF8.GetBytes(message);

            byte[] encryptedBytes = TripleDES_Symm_Algorithm.Encrypt(bytesToEncrypt, secretKey);

            return encryptedBytes;
        }

        public static byte[] EncryptDouble(double amount, string secretKey)
        {
            byte[] bytesToEncrypt = BitConverter.GetBytes(amount);

            byte[] encryptedBytes = TripleDES_Symm_Algorithm.Encrypt(bytesToEncrypt, secretKey);

            return encryptedBytes;
        }
    }
}
