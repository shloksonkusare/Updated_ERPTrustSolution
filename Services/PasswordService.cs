using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ERPTrustSolution.Services;

public class PasswordService : IPasswordService
{
    private readonly string _key = "dotamt";

    public string Encrypt(string clearText)
    {
        try
        {
            string password = "dotCOM Infotech Private Limited, Amravati.";
            byte[] bytes = Encoding.Unicode.GetBytes(clearText);

            using (Aes aes = Aes.Create())
            {
                var pdb = new Rfc2898DeriveBytes(password, new byte[]
                {
                    73,118,97,110,32,77,101,100,118,101,100,101,118
                });

                aes.Key = pdb.GetBytes(32);
                aes.IV = pdb.GetBytes(16);

                using (var ms = new MemoryStream())
                {
                    using (var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
                    {
                        cs.Write(bytes, 0, bytes.Length);
                        cs.Close();
                    }
                    return Convert.ToBase64String(ms.ToArray());
                }
            }
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }
}