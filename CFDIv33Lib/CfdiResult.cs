using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CFDIv33Lib
{
    public class CfdiResult
    {
        public String Mensaje { get; set; }
        public bool Correcto { get; set; }

        public CfdiResult()
        {
            Correcto = true;
        }

        public CfdiResult(Boolean correcto, String mensaje)
        {
            this.Correcto = correcto;
            this.Mensaje = mensaje;
        }
    }
}
