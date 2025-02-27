﻿using System.Collections.Generic;
using System.Threading.Tasks;
using OctoConfig.Core.Arguments;

namespace OctoConfig.Core.Secrets
{
	public interface ISecretsMananger
	{
		Task ReplaceSecrets(List<SecretVariable> vars, IVaultArgs vaultArgs);
	}

	public class SecretsMananger : ISecretsMananger
	{
		private readonly ISecretProviderFactory _providerFact;

		public SecretsMananger(ISecretProviderFactory providerFact)
		{
			_providerFact = providerFact ?? throw new System.ArgumentNullException(nameof(providerFact));
		}

		/// <summary>
		/// Replaces the secrets in the given list that are marked with #{ProviderId:path/to/secret} with their coresponding values
		/// Updates each matching item to be marked as IsSecret
		/// </summary>
		/// <param name="vars">Variables to run the replacement on</param>
		/// <param name="vaultArgs">Vault parameters used to grab the secrets</param>
		public async Task ReplaceSecrets(List<SecretVariable> vars, IVaultArgs vaultArgs)
		{
			if (vaultIsConfigured(vaultArgs))
			{
				foreach (var item in vars)
				{
					var evaluated = item.Value;
					while (hasSecret(evaluated))
					{
						item.IsSecret = true;
						var justVariable = getNextSecret(evaluated);
						(var prefix, var path) = splitSecretAndPrefix(justVariable);
						var secret = await _providerFact.Create(prefix).GetSecret(path).ConfigureAwait(false);
						evaluated = evaluated.Replace($"#{{{justVariable}}}", secret);
					}
					item.Value = evaluated;
				}
			}
		}

		private bool vaultIsConfigured(IVaultArgs vaultArgs)
		{
			return vaultArgs != null && vaultArgs.VaultRoleId != null && vaultArgs.VaultSecretId != null && vaultArgs.VaultUri != null;
		}

		private bool hasSecret(string variable)
		{
			var begin = variable.IndexOf("#{");
			if(begin == -1)
			{
				return false;
			}
			var end = variable.IndexOf('}', begin);
			return end != -1;
		}

		private string getNextSecret(string text)
		{
			var begin = text.IndexOf("#{");
			var end = text.IndexOf('}', begin);
			return text.Substring(begin, end - (begin - 1)).Trim('#', '{', '}');
		}

		private (string, string) splitSecretAndPrefix(string variable)
		{
			if(variable.Contains(":"))
			{
				var ret = variable.Split(':');
				return (ret[0], ret[1]);
			}
			return (SecretProviderFactory.Default, variable);
		}
	}
}
