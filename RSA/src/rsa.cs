﻿namespace pilipala.util.crypto
{
    using System;
    using System.IO;
    using System.Text;
    using System.Linq;
    using System.Numerics;
    using System.Security.Cryptography;
    using System.Text.RegularExpressions;

    /// <summary>
    /// RSA操作类
    /// </summary>
    public class RSA
    {
        /// <summary>
        /// 将密钥对导出成PEM对象，如果convertToPublic含私钥的RSA将只返回公钥，仅含公钥的RSA不受影响
        /// </summary>
        public RSA_PEM ToPEM(bool convertToPublic = false)
        {
            return new RSA_PEM(rsa, convertToPublic);
        }

        /// <summary>
        /// 加密字符串（utf-8），出错抛异常
        /// </summary>
        public string Encode(string str, RSAEncryptionPadding paddingMode)
        {
            return Convert.ToBase64String(Encode(Encoding.UTF8.GetBytes(str), paddingMode));
        }

        /// <summary>
        /// 加密数据，出错抛异常
        /// </summary>
        public byte[] Encode(byte[] data, RSAEncryptionPadding paddingMode)
        {
            var blockLen = rsa.KeySize / 8 - 11;

            if (data.Length <= blockLen)
                return rsa.Encrypt(data, paddingMode);


            using var dataStream = new MemoryStream(data);
            using var enStream = new MemoryStream();
            var buffer = new byte[blockLen];
            int len = dataStream.Read(buffer, 0, blockLen);

            while (len > 0)
            {
                var block = new byte[len];
                Array.Copy(buffer, 0, block, 0, len);

                var enBlock = rsa.Encrypt(block, RSAEncryptionPadding.Pkcs1);
                enStream.Write(enBlock, 0, enBlock.Length);

                len = dataStream.Read(buffer, 0, blockLen);
            }

            return enStream.ToArray();
        }

        /// <summary>
        /// 解密字符串（utf-8），解密异常返回null
        /// </summary>
        public string DecodeOrNull(string str, RSAEncryptionPadding paddingMode)
        {
            if (string.IsNullOrEmpty(str))
                return null;

            byte[] bytes = null;
            try
            {
                bytes = Convert.FromBase64String(str);
            }
            catch
            {
            }

            if (bytes == null)
                return null;

            var val = DecodeOrNull(bytes, paddingMode);

            if (val == null)
                return null;

            return Encoding.UTF8.GetString(val);
        }

        /// <summary>
        /// 解密数据，解密异常返回null
        /// </summary>
        public byte[] DecodeOrNull(byte[] data, RSAEncryptionPadding paddingMode)
        {
            try
            {
                var blockLen = rsa.KeySize / 8;
                if (data.Length <= blockLen)
                    return rsa.Decrypt(data, RSAEncryptionPadding.Pkcs1);


                using var dataStream = new MemoryStream(data);
                using var deStream = new MemoryStream();
                var buffer = new byte[blockLen];
                var len = dataStream.Read(buffer, 0, blockLen);

                while (len > 0)
                {
                    var block = new byte[len];
                    Array.Copy(buffer, 0, block, 0, len);

                    var deBlock = rsa.Decrypt(block, paddingMode);
                    deStream.Write(deBlock, 0, deBlock.Length);

                    len = dataStream.Read(buffer, 0, blockLen);
                }

                return deStream.ToArray();
            }
            catch
            {
                return null;
            }
        }

        private readonly System.Security.Cryptography.RSA rsa;

        /// <summary>
        /// 最底层的RSACryptoServiceProvider对象
        /// </summary>
        public System.Security.Cryptography.RSA RSAObject => rsa;


        /// <summary>
        /// 密钥位数
        /// </summary>
        public int KeySize => rsa.KeySize;

        /// <summary>
        /// 用指定密钥大小创建一个新的RSA，出错抛异常
        /// </summary>
        public RSA(int keySize)
        {
            rsa = System.Security.Cryptography.RSA.Create(keySize);
        }

        /// <summary>
        /// 通过一个pem文件创建RSA，pem为公钥或私钥，出错抛异常
        /// </summary>
        public RSA(string pem)
        {
            rsa = RSA_PEM.FromPEM(pem).GetRSA();
        }

        /// <summary>
        /// 通过一个pem对象创建RSA，pem为公钥或私钥，出错抛异常
        /// </summary>
        public RSA(RSA_PEM pem)
        {
            rsa = pem.GetRSA();
        }

        /// <summary>
        /// 本方法会先生成RSA_PEM再创建RSA：通过公钥指数和私钥指数构造一个PEM，会反推计算出P、Q但和原始生成密钥的P、Q极小可能相同
        /// 注意：所有参数首字节如果是0，必须先去掉
        /// 出错将会抛出异常
        /// </summary>
        /// <param name="modulus">必须提供模数</param>
        /// <param name="exponent">必须提供公钥指数</param>
        /// <param name="dOrNull">私钥指数可以不提供，导出的PEM就只包含公钥</param>
        public RSA(byte[] modulus, byte[] exponent, byte[] dOrNull)
        {
            rsa = new RSA_PEM(modulus, exponent, dOrNull).GetRSA();
        }

        /// <summary>
        /// 本方法会先生成RSA_PEM再创建RSA：通过全量的PEM字段数据构造一个PEM，除了模数modulus和公钥指数exponent必须提供外，其他私钥指数信息要么全部提供，要么全部不提供（导出的PEM就只包含公钥）
        /// 注意：所有参数首字节如果是0，必须先去掉
        /// </summary>
        public RSA(byte[] modulus, byte[] exponent, byte[] d, byte[] p, byte[] q, byte[] dp, byte[] dq, byte[] inverseQ)
        {
            rsa = new RSA_PEM(modulus, exponent, d, p, q, dp, dq, inverseQ).GetRSA();
        }
    }

    /// <summary>
    /// RSA_PEM格式密钥对的解析和导出
    /// </summary>
    public class RSA_PEM
    {
        /// <summary>
        /// modulus 模数n，公钥、私钥都有
        /// </summary>
        private byte[] Key_Modulus;

        /// <summary>
        /// publicExponent 公钥指数e，公钥、私钥都有
        /// </summary>
        private byte[] Key_Exponent;

        /// <summary>
        /// privateExponent 私钥指数d，只有私钥的时候才有
        /// </summary>
        private byte[] Key_D;

        /// <summary>
        /// prime1
        /// </summary>
        private byte[] Val_P;

        /// <summary>
        /// prime2
        /// </summary>
        private byte[] Val_Q;

        /// <summary>
        /// exponent1
        /// </summary>
        private byte[] Val_DP;

        /// <summary>
        /// exponent2
        /// </summary>
        private byte[] Val_DQ;

        /// <summary>
        /// coefficient
        /// </summary>
        private byte[] Val_InverseQ;

        private RSA_PEM()
        {
        }

        /// <summary>
        /// 通过RSA中的公钥和私钥构造一个PEM，如果convertToPublic含私钥的RSA将只读取公钥，仅含公钥的RSA不受影响
        /// </summary>
        public RSA_PEM(System.Security.Cryptography.RSA rsa, bool convertToPublic = false)
        {
            var isPublic = convertToPublic;
            var param = rsa.ExportParameters(!isPublic);

            Key_Modulus = param.Modulus;
            Key_Exponent = param.Exponent;

            if (!isPublic)
            {
                Key_D = param.D;

                Val_P = param.P;
                Val_Q = param.Q;
                Val_DP = param.DP;
                Val_DQ = param.DQ;
                Val_InverseQ = param.InverseQ;
            }
        }

        /// <summary>
        /// 通过全量的PEM字段数据构造一个PEM，除了模数modulus和公钥指数exponent必须提供外，其他私钥指数信息要么全部提供，要么全部不提供（导出的PEM就只包含公钥）
        /// 注意：所有参数首字节如果是0，必须先去掉
        /// </summary>
        public RSA_PEM(byte[] modulus, byte[] exponent, byte[] d, byte[] p, byte[] q, byte[] dp, byte[] dq,
            byte[] inverseQ)
        {
            Key_Modulus = modulus;
            Key_Exponent = exponent;
            Key_D = BigL(d, modulus.Length);

            int keyLen = modulus.Length / 2;
            Val_P = BigL(p, keyLen);
            Val_Q = BigL(q, keyLen);
            Val_DP = BigL(dp, keyLen);
            Val_DQ = BigL(dq, keyLen);
            Val_InverseQ = BigL(inverseQ, keyLen);
        }

        /// <summary>
        /// 通过公钥指数和私钥指数构造一个PEM，会反推计算出P、Q但和原始生成密钥的P、Q极小可能相同
        /// 注意：所有参数首字节如果是0，必须先去掉
        /// 出错将会抛出异常
        /// </summary>
        /// <param name="modulus">必须提供模数</param>
        /// <param name="exponent">必须提供公钥指数</param>
        /// <param name="dOrNull">私钥指数可以不提供，导出的PEM就只包含公钥</param>
        public RSA_PEM(byte[] modulus, byte[] exponent, byte[] dOrNull)
        {
            Key_Modulus = modulus; //modulus
            Key_Exponent = exponent; //publicExponent

            if (dOrNull != null)
            {
                Key_D = BigL(dOrNull, modulus.Length); //privateExponent

                //反推P、Q
                BigInteger n = BigX(modulus);
                BigInteger e = BigX(exponent);
                BigInteger d = BigX(dOrNull);
                BigInteger p = FindFactor(e, d, n);
                BigInteger q = n / p;

                if (p.CompareTo(q) > 0)
                    (p, q) = (q, p);

                BigInteger exp1 = d % (p - BigInteger.One);
                BigInteger exp2 = d % (q - BigInteger.One);
                BigInteger coeff = BigInteger.ModPow(q, p - 2, p);

                var keyLen = modulus.Length / 2;
                Val_P = BigL(BigB(p), keyLen); //prime1
                Val_Q = BigL(BigB(q), keyLen); //prime2
                Val_DP = BigL(BigB(exp1), keyLen); //exponent1
                Val_DQ = BigL(BigB(exp2), keyLen); //exponent2
                Val_InverseQ = BigL(BigB(coeff), keyLen); //coefficient
            }
        }

        /// <summary>
        /// 密钥位数
        /// </summary>
        public int KeySize
        {
            get { return Key_Modulus.Length * 8; }
        }

        /// <summary>
        /// 将PEM中的公钥私钥转成RSA对象，如果未提供私钥，RSA中就只包含公钥
        /// </summary>
        public System.Security.Cryptography.RSA GetRSA()
        {
            var rsa = System.Security.Cryptography.RSA.Create();

            var param = new RSAParameters
            {
                Modulus = Key_Modulus,
                Exponent = Key_Exponent
            };
            if (Key_D != null)
            {
                param.D = Key_D;
                param.P = Val_P;
                param.Q = Val_Q;
                param.DP = Val_DP;
                param.DQ = Val_DQ;
                param.InverseQ = Val_InverseQ;
            }

            rsa.ImportParameters(param);
            return rsa;
        }

        /// <summary>
        /// 转成正整数，如果是负数，需要加前导0转成正整数
        /// </summary>
        static public BigInteger BigX(byte[] bigb)
        {
            if (bigb[0] > 0x7F)
            {
                byte[] c = new byte[bigb.Length + 1];
                Array.Copy(bigb, 0, c, 1, bigb.Length);
                bigb = c;
            }

            return new BigInteger(bigb.Reverse().ToArray()); //C#的二进制是反的
        }

        /// <summary>
        /// BigInt导出byte整数首字节>0x7F的会加0前导，保证正整数，因此需要去掉0
        /// </summary>
        static public byte[] BigB(BigInteger bigx)
        {
            byte[] val = bigx.ToByteArray().Reverse().ToArray(); //C#的二进制是反的
            if (val[0] == 0)
            {
                byte[] c = new byte[val.Length - 1];
                Array.Copy(val, 1, c, 0, c.Length);
                val = c;
            }

            return val;
        }

        /// <summary>
        /// 某些密钥参数可能会少一位（32个byte只有31个，目测是密钥生成器的问题，只在c#生成的密钥中发现这种参数，java中生成的密钥没有这种现象），直接修正一下就行；这个问题与BigB有本质区别，不能动BigB
        /// </summary>
        static public byte[] BigL(byte[] bytes, int keyLen)
        {
            if (keyLen - bytes.Length == 1)
            {
                byte[] c = new byte[bytes.Length + 1];
                Array.Copy(bytes, 0, c, 1, bytes.Length);
                bytes = c;
            }

            return bytes;
        }

        /// <summary>
        /// 由n e d 反推 P Q 
        /// 资料： https://stackoverflow.com/questions/43136036/how-to-get-a-rsaprivatecrtkey-from-a-rsaprivatekey
        /// https://v2ex.com/t/661736
        /// </summary>
        static private BigInteger FindFactor(BigInteger e, BigInteger d, BigInteger n)
        {
            BigInteger edMinus1 = e * d - BigInteger.One;

            var s = -1;

            if (edMinus1 != BigInteger.Zero)
                s = (int) (BigInteger.Log(edMinus1 & -edMinus1) / BigInteger.Log(2));


            BigInteger t = edMinus1 >> s;

            var now = DateTime.Now.Ticks;
            for (var aInt = 2; true; aInt++)
            {
                if (aInt % 10 == 0 && DateTime.Now.Ticks - now > 3000 * 10000)
                    throw new Exception("推算RSA.P超时"); //测试最多循环2次，1024位的速度很快 8ms


                BigInteger aPow = BigInteger.ModPow(new BigInteger(aInt), t, n);
                for (var i = 1; i <= s; i++)
                {
                    if (aPow == BigInteger.One || aPow == n - BigInteger.One)
                        break;

                    BigInteger aPowSquared = aPow * aPow % n;
                    if (aPowSquared == BigInteger.One)
                    {
                        return BigInteger.GreatestCommonDivisor(aPow - BigInteger.One, n);
                    }

                    aPow = aPowSquared;
                }
            }
        }

        /// <summary>
        /// 用PEM格式密钥对创建RSA，支持PKCS#1、PKCS#8格式的PEM
        /// 出错将会抛出异常
        /// </summary>
        static public RSA_PEM FromPEM(string pem)
        {
            var param = new RSA_PEM();

            var base64 = _PEMCode.Replace(pem, "");
            byte[] data = null;
            try
            {
                data = Convert.FromBase64String(base64);
            }
            catch
            {
            }

            if (data == null)
                throw new Exception("PEM内容无效");

            var idx = 0;

            //读取长度
            int readLen(byte first)
            {
                if (data[idx] == first)
                {
                    idx++;

                    if (data[idx] == 0x81)
                    {
                        idx++;
                        return data[idx++];
                    }

                    if (data[idx] == 0x82)
                    {
                        idx++;
                        return (data[idx++] << 8) + data[idx++];
                    }

                    if (data[idx] < 0x80)
                    {
                        return data[idx++];
                    }
                }

                throw new Exception("PEM未能提取到数据");
            }

            //读取块数据
            byte[] readBlock()
            {
                var len = readLen(0x02);
                if (data[idx] == 0x00)
                {
                    idx++;
                    len--;
                }

                var val = new byte[len];

                for (var i = 0; i < len; i++)
                    val[i] = data[idx + i];

                idx += len;
                return val;
            }

            //比较data从idx位置开始是否是byts内容
            bool eq(byte[] byts)
            {
                for (var i = 0; i < byts.Length; i++, idx++)
                {
                    if (idx >= data.Length)
                        return false;

                    if (byts[i] != data[idx])
                        return false;
                }

                return true;
            }


            if (pem.Contains("PUBLIC KEY"))
            {
                /****使用公钥****/
                //读取数据总长度
                readLen(0x30);

                //检测PKCS8
                var idx2 = idx;
                if (eq(_SeqOID))
                {
                    //读取1长度
                    readLen(0x03);
                    idx++; //跳过0x00
                    //读取2长度
                    readLen(0x30);
                }
                else
                {
                    idx = idx2;
                }

                //Modulus
                param.Key_Modulus = readBlock();

                //Exponent
                param.Key_Exponent = readBlock();
            }
            else if (pem.Contains("PRIVATE KEY"))
            {
                /****使用私钥****/
                //读取数据总长度
                readLen(0x30);

                //读取版本号
                if (!eq(_Ver))
                    throw new Exception("PEM未知版本");


                //检测PKCS8
                var idx2 = idx;
                if (eq(_SeqOID))
                {
                    //读取1长度
                    readLen(0x04);
                    //读取2长度
                    readLen(0x30);

                    //读取版本号
                    if (!eq(_Ver))
                        throw new Exception("PEM版本无效");
                }
                else
                    idx = idx2;


                //读取数据
                param.Key_Modulus = readBlock();
                param.Key_Exponent = readBlock();
                var keyLen = param.Key_Modulus.Length;
                param.Key_D = BigL(readBlock(), keyLen);
                keyLen /= 2;
                param.Val_P = BigL(readBlock(), keyLen);
                param.Val_Q = BigL(readBlock(), keyLen);
                param.Val_DP = BigL(readBlock(), keyLen);
                param.Val_DQ = BigL(readBlock(), keyLen);
                param.Val_InverseQ = BigL(readBlock(), keyLen);
            }
            else
            {
                throw new Exception("pem需要BEGIN END标头");
            }

            return param;
        }

        static private readonly Regex _PEMCode = new(@"--+.+?--+|\s+");

        static private readonly byte[] _SeqOID = new byte[]
            {0x30, 0x0D, 0x06, 0x09, 0x2A, 0x86, 0x48, 0x86, 0xF7, 0x0D, 0x01, 0x01, 0x01, 0x05, 0x00};

        static private readonly byte[] _Ver = new byte[] {0x02, 0x01, 0x00};

        /// <summary>
        /// 将RSA中的密钥对转换成PEM PKCS#1格式
        /// 。convertToPublic：等于true时含私钥的RSA将只返回公钥，仅含公钥的RSA不受影响
        /// 。公钥如：-----BEGIN RSA PUBLIC KEY-----，私钥如：-----BEGIN RSA PRIVATE KEY-----
        /// 。似乎导出PKCS#1公钥用的比较少，PKCS#8的公钥用的多些，私钥#1#8都差不多
        /// </summary>
        public string ToPEM_PKCS1(bool convertToPublic = false)
        {
            return ToPEM(convertToPublic, false, false);
        }

        /// <summary>
        /// 将RSA中的密钥对转换成PEM PKCS#8格式
        /// 。convertToPublic：等于true时含私钥的RSA将只返回公钥，仅含公钥的RSA不受影响
        /// 。公钥如：-----BEGIN PUBLIC KEY-----，私钥如：-----BEGIN PRIVATE KEY-----
        /// </summary>
        public string ToPEM_PKCS8(bool convertToPublic = false)
        {
            return ToPEM(convertToPublic, true, true);
        }

        /// <summary>
        /// 将RSA中的密钥对转换成PEM格式
        /// 。convertToPublic：等于true时含私钥的RSA将只返回公钥，仅含公钥的RSA不受影响
        /// 。privateUsePKCS8：私钥的返回格式，等于true时返回PKCS#8格式（-----BEGIN PRIVATE KEY-----），否则返回PKCS#1格式（-----BEGIN RSA PRIVATE KEY-----），返回公钥时此参数无效；两种格式使用都比较常见
        /// 。publicUsePKCS8：公钥的返回格式，等于true时返回PKCS#8格式（-----BEGIN PUBLIC KEY-----），否则返回PKCS#1格式（-----BEGIN RSA PUBLIC KEY-----），返回私钥时此参数无效；一般用的多的是true PKCS#8格式公钥，PKCS#1格式似乎比较少见公钥
        /// </summary>
        public string ToPEM(bool convertToPublic, bool privateUsePKCS8, bool publicUsePKCS8)
        {
            //https://www.jianshu.com/p/25803dd9527d
            //https://www.cnblogs.com/ylz8401/p/8443819.html
            //https://blog.csdn.net/jiayanhui2877/article/details/47187077
            //https://blog.csdn.net/xuanshao_/article/details/51679824
            //https://blog.csdn.net/xuanshao_/article/details/51672547

            var ms = new MemoryStream();

            //写入一个长度字节码
            void writeLenByte(int len)
            {
                if (len < 0x80)
                    ms.WriteByte((byte) len);
                else if (len <= 0xff)
                {
                    ms.WriteByte(0x81);
                    ms.WriteByte((byte) len);
                }
                else
                {
                    ms.WriteByte(0x82);
                    ms.WriteByte((byte) (len >> 8 & 0xff));
                    ms.WriteByte((byte) (len & 0xff));
                }
            }

            //写入一块数据
            void writeBlock(byte[] byts)
            {
                var addZero = (byts[0] >> 4) >= 0x8;
                ms.WriteByte(0x02);
                var len = byts.Length + (addZero ? 1 : 0);
                writeLenByte(len);

                if (addZero)
                    ms.WriteByte(0x00);


                ms.Write(byts, 0, byts.Length);
            }

            //根据后续内容长度写入长度数据
            byte[] writeLen(int index, byte[] byts)
            {
                var len = byts.Length - index;

                ms.SetLength(0);
                ms.Write(byts, 0, index);
                writeLenByte(len);
                ms.Write(byts, index, len);

                return ms.ToArray();
            }

            void writeAll(MemoryStream stream, byte[] byts)
            {
                stream.Write(byts, 0, byts.Length);
            }

            string TextBreak(string text, int line)
            {
                var idx = 0;
                var len = text.Length;
                var str = new StringBuilder();
                while (idx < len)
                {
                    if (idx > 0)
                        str.Append('\n');

                    if (idx + line >= len)
                        str.Append(text[idx..]);
                    else
                        str.Append(text.Substring(idx, line));

                    idx += line;
                }

                return str.ToString();
            }


            if (Key_D == null || convertToPublic)
            {
                /****生成公钥****/

                //写入总字节数，不含本段长度，额外需要24字节的头，后续计算好填入
                ms.WriteByte(0x30);
                var index1 = (int) ms.Length;

                //PKCS8 多一段数据
                int index2 = -1, index3 = -1;
                if (publicUsePKCS8)
                {
                    //固定内容
                    // encoded OID sequence for PKCS #1 rsaEncryption szOID_RSA_RSA = "1.2.840.113549.1.1.1"
                    writeAll(ms, _SeqOID);

                    //从0x00开始的后续长度
                    ms.WriteByte(0x03);
                    index2 = (int) ms.Length;
                    ms.WriteByte(0x00);

                    //后续内容长度
                    ms.WriteByte(0x30);
                    index3 = (int) ms.Length;
                }

                //写入Modulus
                writeBlock(Key_Modulus);

                //写入Exponent
                writeBlock(Key_Exponent);


                //计算空缺的长度
                var byts = ms.ToArray();

                if (index2 != -1)
                {
                    byts = writeLen(index3, byts);
                    byts = writeLen(index2, byts);
                }

                byts = writeLen(index1, byts);


                var flag = " PUBLIC KEY";
                if (!publicUsePKCS8)
                    flag = " RSA" + flag;


                return $"-----BEGIN{flag}-----\n" +
                       TextBreak(Convert.ToBase64String(byts), 64) +
                       $"\n-----END{flag}-----";
            }
            else
            {
                /****生成私钥****/

                //写入总字节数，后续写入
                ms.WriteByte(0x30);
                var index1 = (int) ms.Length;

                //写入版本号
                writeAll(ms, _Ver);

                //PKCS8 多一段数据
                var index2 = -1;
                var index3 = -1;
                if (privateUsePKCS8)
                {
                    //固定内容
                    writeAll(ms, _SeqOID);

                    //后续内容长度
                    ms.WriteByte(0x04);
                    index2 = (int) ms.Length;

                    //后续内容长度
                    ms.WriteByte(0x30);
                    index3 = (int) ms.Length;

                    //写入版本号
                    writeAll(ms, _Ver);
                }

                //写入数据
                writeBlock(Key_Modulus);
                writeBlock(Key_Exponent);
                writeBlock(Key_D);
                writeBlock(Val_P);
                writeBlock(Val_Q);
                writeBlock(Val_DP);
                writeBlock(Val_DQ);
                writeBlock(Val_InverseQ);


                //计算空缺的长度
                var byts = ms.ToArray();

                if (index2 != -1)
                {
                    byts = writeLen(index3, byts);
                    byts = writeLen(index2, byts);
                }

                byts = writeLen(index1, byts);

                var flag = " PRIVATE KEY";
                if (!privateUsePKCS8)
                    flag = " RSA" + flag;

                return $"-----BEGIN{flag}-----\n" +
                       TextBreak(Convert.ToBase64String(byts), 64) +
                       $"\n-----END{flag}-----";
            }
        }
    }
}