using Newtonsoft.Json.Linq;

namespace ApiImagenesAppVoluntario.Helpers
{
    public static class ResponseHelper
    {
        public static int ObtenerCodigoEstatusRespuesta(string jsonResultado, int codigoEstatusExitoso)
        {
            JObject objetoJsonResultado = JObject.Parse(jsonResultado);
            int codigoEstatus = codigoEstatusExitoso;
            
            if (objetoJsonResultado.SelectToken("$.errors[0].status") != null)
            {
                JValue? valorCodigoEstatus = objetoJsonResultado.SelectToken("$.errors[0].status") as JValue;
                codigoEstatus = valorCodigoEstatus is not null 
                    ? Convert.ToInt32(valorCodigoEstatus.Value) 
                    : 400;
            }
            
            return codigoEstatus;
        }
    }
}
