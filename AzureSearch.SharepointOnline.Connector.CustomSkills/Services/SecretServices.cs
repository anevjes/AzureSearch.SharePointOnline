using Microsoft.Azure.KeyVault;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BishopBlobCustomSkill.Services
{
    public class SecretServices
    {
        private KeyVaultClient kvc;
        public SecretServices(String securityToken)
        {
           // kvc = new KeyVaultClient(new KeyVaultClient.AuthenticationCallback(securityToken));
           //var kvc = new KeyVaultClient()

        }
    }
}
