import { Injectable } from '@angular/core';

export interface FiscalCertificate {
  validUntil?: string;
  fileName: string;
  base64: string;
  password: string;
  uf: 'RS';
  environment: 'homologacao';
}

export interface FiscalTaxSettings {
  regime: string;
  icmsCode: string;
  pisCode: string;
  cofinsCode: string;
  icmsRate: number | null;
  pisRate: number | null;
  cofinsRate: number | null;
}

@Injectable({ providedIn: 'root' })
export class FiscalCertificateService {
  private readonly key = 'uniflow:certificado:empresa-1';
  private readonly taxKey = 'uniflow:tributacao:empresa-1';

  loadTax(): FiscalTaxSettings | null {
    const raw = localStorage.getItem(this.taxKey);
    return raw ? JSON.parse(raw) as FiscalTaxSettings : null;
  }

  saveTax(value: FiscalTaxSettings): void { localStorage.setItem(this.taxKey, JSON.stringify(value)); }

  load(): FiscalCertificate | null {
    const raw = localStorage.getItem(this.key);
    if (!raw) return null;
    const value = JSON.parse(raw) as FiscalCertificate;
    if (typeof value.base64 !== 'string' || typeof value.password !== 'string' || typeof value.fileName !== 'string' || value.uf !== 'RS' || value.environment !== 'homologacao') {
      throw new Error('Certificado salvo inválido. Cadastre novamente.');
    }
    return value;
  }

  save(value: FiscalCertificate): void { localStorage.setItem(this.key, JSON.stringify(value)); }
  remove(): void { localStorage.removeItem(this.key); }
}
