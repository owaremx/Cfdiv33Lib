using Chilkat;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using System.Xml.Xsl;

namespace CFDIv33Lib
{
    public partial class Comprobante
    {
        [System.Xml.Serialization.XmlIgnore]
        public String RutaXSLTCadenaOriginal { get; set; }
        [System.Xml.Serialization.XmlIgnore]
        public String RutaXSDCfdi { get; set; }

        public Comprobante() {
            this.versionField = "3.3";
            Conceptos = new List<ComprobanteConcepto>();
        }

        public Comprobante(String RutaXSLTCadenaOriginal, String RutaXSDCfdi)
        {
            //procesamiento de impuestos
            this.versionField = "3.3";
            Conceptos = new List<ComprobanteConcepto>();
            this.RutaXSLTCadenaOriginal = RutaXSLTCadenaOriginal;
            this.RutaXSDCfdi = RutaXSDCfdi;
        }

        public CfdiResult AgregarCertificado(String rutaCert)
        {
            Cert cert = new Cert() ;
            if (!cert.LoadFromFile(rutaCert))
                return new CfdiResult() { Mensaje = cert.LastErrorText, Correcto = false };

            if(cert.Expired)
                return new CfdiResult() { Mensaje = "Certificado expirado", Correcto = false };

            String serie = cert.SerialNumber;

            String noCertificado = "";
            for (int i = 1; i < serie.Length; i += 2)
                noCertificado += serie[i];

            this.Certificado = cert.GetEncoded().Replace(Environment.NewLine, "");
            this.NoCertificado = noCertificado;

            return new CfdiResult();
        }

        private String GetCadenaOriginal(String rutaXsltCadenaOriginal)
        {
            String rutaComprobanteTemp = Path.GetTempFileName();
            String rutaSalidaTemp = Path.GetTempFileName();
            //hguardar el xml del comprobante sin sellar para poder obtener la cadena original
            EscribirXml(rutaComprobanteTemp, false, "","","","");

            XslCompiledTransform xsl = new XslCompiledTransform();
            xsl.Load(rutaXsltCadenaOriginal);
            xsl.Transform(rutaComprobanteTemp, rutaSalidaTemp);
            xsl = null;

            StreamReader reader = new StreamReader(rutaSalidaTemp);
            String cadena = reader.ReadToEnd();
            reader.Close();
            reader.Dispose();
            reader = null;

            //eliminar archivos temporales
            File.Delete(rutaComprobanteTemp);
            File.Delete(rutaSalidaTemp);

            return cadena;
        }

        public CfdiResult SellarXml(String rutaCert, String rutaKey, String contrasena)
        {
            CfdiResult result = AgregarCertificado(rutaCert);
            if (!result.Correcto)
                return result;

            PrivateKey privKey = new PrivateKey();

            String cadenaOriginal = GetCadenaOriginal(this.RutaXSLTCadenaOriginal);

            if(!privKey.LoadPkcs8EncryptedFile(rutaKey, contrasena))
            {
                return new CfdiResult(false, privKey.LastErrorText);
            }

            String privKeyXml = privKey.GetXml();
            Rsa rsa = new Rsa();
            String hashAlg = "SHA-256";

            if (!rsa.UnlockComponent("RSAT34MB34N_7F1CD986683M"))
            {
                return new CfdiResult(false, rsa.LastErrorText);
            }

            if (!rsa.ImportPrivateKey(privKeyXml))
            {
                return new CfdiResult(false, rsa.LastErrorText);
            }

            rsa.Charset = "UTF-8";
            rsa.EncodingMode = "base64";
            rsa.LittleEndian = false;

            Cert cert = new Cert();
            if(!cert.LoadFromFile(rutaCert))
                return new CfdiResult(false, cert.LastErrorText);

            String selloBase64 = rsa.SignStringENC(cadenaOriginal, hashAlg);

            PublicKey publicKey = cert.ExportPublicKey();

            Rsa rsa2 = new Rsa();
            if (!rsa2.ImportPublicKey(publicKey.GetXml()))
            {
                return new CfdiResult(false, rsa2.LastErrorText);
            }
            rsa2.Charset = "utf-8";
            rsa2.EncodingMode = "base64";
            rsa2.LittleEndian = false;

            if(!rsa2.VerifyStringENC(cadenaOriginal, hashAlg, selloBase64))
            {
                return new CfdiResult(false, rsa2.LastErrorText);
            }

            cert.Dispose();
            privKey.Dispose();
            publicKey.Dispose();

            this.Sello = selloBase64;

            return new CfdiResult();
        }

        private void ValidarXML(String rutaXml)
        {
            XmlReaderSettings settings = new XmlReaderSettings();
            settings.Schemas.Add("http://www.sat.gob.mx/cfd/3", RutaXSDCfdi);
            //settings.Schemas.Add("http://www.sat.gob.mx/implocal", IO.Path.Combine(Path, "XSD\implocal.xsd"))
            settings.ValidationType = ValidationType.Schema;
            settings.ValidationFlags = XmlSchemaValidationFlags.ReportValidationWarnings;

            //settings.ValidationEventHandler += validacionXml_Settings_ValidationEventHandler;

            XmlReader reader = XmlReader.Create(rutaXml, settings);
            while (reader.Read()) ;

            reader.Close();
        }

        private void validacionXml_Settings_ValidationEventHandler(object sender, ValidationEventArgs e)
        {
            throw new NotImplementedException();
        }

        public CfdiResult GenerarXml(String rutaSalida, String rutaCert, String rutaKey, String contrasena)
        {
            return EscribirXml(rutaSalida, true, rutaCert, rutaKey, contrasena, RutaXSLTCadenaOriginal); 
        }

        private CfdiResult EscribirXml(String rutaSalida, bool sellar, String rutaCert, String rutaKey, String contrasena, String rutaXsltCadenaOriginal)
        {
            decimal subtotal = 0;
            decimal impuestosTrasladados = 0;
            foreach (ComprobanteConcepto concepto in conceptosField)
            {
                subtotal += concepto.Importe;
                foreach (ComprobanteConceptoImpuestosTraslado i in concepto.Impuestos.Traslados)
                    impuestosTrasladados += i.Importe;
            }

            ComprobanteImpuestosTraslado cImpuestoIVA = new ComprobanteImpuestosTraslado();
            cImpuestoIVA.Importe = impuestosTrasladados;
            cImpuestoIVA.TasaOCuota = Conceptos[0].Impuestos.Traslados[0].TasaOCuota;
            cImpuestoIVA.Impuesto = Conceptos[0].Impuestos.Traslados[0].Impuesto;

            this.SubTotal = subtotal;
            this.Total = subtotal + impuestosTrasladados - this.Descuento;
            this.DescuentoSpecified = true;

            this.Impuestos = new ComprobanteImpuestos()
            {
                TotalImpuestosRetenidos = 0,
                TotalImpuestosRetenidosSpecified = false,
                TotalImpuestosTrasladados = impuestosTrasladados,
                TotalImpuestosTrasladadosSpecified = true,
                Traslados = new ComprobanteImpuestosTraslado[] { cImpuestoIVA }
            };

            if (sellar)
            {
                CfdiResult r= SellarXml(rutaCert, rutaKey, contrasena);
                if (!r.Correcto)
                    return r;
            }

            XmlSerializerNamespaces xNs = new XmlSerializerNamespaces();
            xNs.Add("cfdi", "http://www.sat.gob.mx/cfd/3");
            xNs.Add("xsi", "http://www.w3.org/2001/XMLSchema-instance");

            XmlSerializer serializer = new XmlSerializer(typeof(Comprobante));
            XmlTextWriter writer = new XmlTextWriter(rutaSalida, Encoding.UTF8);
            writer.Formatting = Formatting.Indented;
            writer.Indentation = 4;
            serializer.Serialize(writer, this, xNs);
            writer.Close();

            if (sellar)
                ValidarXML(rutaSalida);

            return new CfdiResult();
        }
        
    }
}
