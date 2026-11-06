import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { FiscalCertificateService, FiscalTaxSettings } from '../../core/fiscal-certificate.service';
import { ApiService } from '../../core/api.service';
import { AppIconComponent } from '../../shared/app-icon/app-icon.component';

@Component({
  selector: 'app-fiscal-settings',
  imports: [FormsModule, AppIconComponent],
  templateUrl: './fiscal-settings.component.html',
  styleUrl: './fiscal-settings.component.scss',
})
export class FiscalSettingsComponent {
  private readonly store = inject(FiscalCertificateService);
  private readonly api = inject(ApiService);
  protected readonly message = signal('');
  protected readonly busy = signal(false);
  protected readonly savedName = signal('');
  protected readonly activeTab = signal<'all' | 'certificate' | 'tax'>('all');
  protected readonly showUpload = signal(false);
  protected readonly validUntil = signal('');
  protected password = '';
  protected file: File | null = null;
  protected fileInput: HTMLInputElement | null = null;
  protected tax: FiscalTaxSettings = { regime: '', icmsCode: '', pisCode: '', cofinsCode: '', icmsRate: null, pisRate: null, cofinsRate: null };
  protected readonly taxMessage = signal('');

  constructor() {
    try {
      const certificate = this.store.load();
      this.savedName.set(certificate?.fileName ?? '');
      this.validUntil.set(certificate?.validUntil ?? '');
      this.showUpload.set(!certificate);
    }
    catch { this.message.set('Não foi possível ler o certificado salvo. Cadastre novamente.'); }
    try { this.tax = this.store.loadTax() ?? this.tax; }
    catch { this.taxMessage.set('Não foi possível ler a configuração tributária. Cadastre novamente.'); }
  }

  protected restoreTax(): void {
    try {
      this.tax = this.store.loadTax() ?? { regime: '', icmsCode: '', pisCode: '', cofinsCode: '', icmsRate: null, pisRate: null, cofinsRate: null };
      this.taxMessage.set('Última configuração salva restaurada.');
    } catch { this.taxMessage.set('Não foi possível restaurar a configuração salva.'); }
  }

  protected certificateStatus(): string {
    if (!this.savedName()) return 'Não configurado';
    if (!this.validUntil()) return 'Certificado cadastrado';
    return new Date(this.validUntil()).getTime() > Date.now() ? 'Configurado e válido' : 'Certificado vencido';
  }

  protected certificateExpiry(): string {
    return this.validUntil() ? new Date(this.validUntil()).toLocaleDateString('pt-BR') : 'Valide novamente para consultar';
  }

  protected saveTax(): void {
    const simple = this.tax.regime === '1' || this.tax.regime === '4';
    const codes = simple ? ['101', '102', '103', '201', '202', '203', '300', '400', '500', '900'] : ['00', '02', '10', '15', '20', '30', '40', '41', '50', '51', '53', '60', '61', '70', '90'];
    if (!['1', '2', '3', '4'].includes(this.tax.regime) || !codes.includes(this.tax.icmsCode) || !/^(0[1-9]|49|5[0-6]|6[0-7]|7[0-5]|98|99)$/.test(this.tax.pisCode) || !/^(0[1-9]|49|5[0-6]|6[0-7]|7[0-5]|98|99)$/.test(this.tax.cofinsCode)) {
      this.taxMessage.set('Selecione o regime e informe os códigos fiscais válidos, conforme orientação do contador.'); return;
    }
    if ([this.tax.icmsRate, this.tax.pisRate, this.tax.cofinsRate].some(rate => rate !== null && (!Number.isFinite(rate) || rate < 0 || rate > 100))) {
      this.taxMessage.set('As alíquotas devem estar entre 0 e 100%.'); return;
    }
    try { this.store.saveTax(this.tax); this.taxMessage.set('Configuração tributária salva neste navegador. A tributação deve ser revisada por produto e operação antes da emissão.'); }
    catch { this.taxMessage.set('Não foi possível salvar a configuração tributária.'); }
  }

  protected selectFile(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.fileInput = input;
    this.file = input.files?.[0] ?? null;
    this.message.set('');
  }

  protected async save(): Promise<void> {
    if (this.busy()) return;
    const file = this.file;
    if (!file || !/\.(pfx|p12)$/i.test(file.name) || file.size > 1024 * 1024 || file.size === 0) {
      this.message.set('Selecione um certificado A1 .pfx ou .p12 de até 1 MB.'); return;
    }
    if (!this.password) { this.message.set('Informe a senha do certificado.'); return; }
    this.busy.set(true); this.message.set('');
    try {
      const bytes = new Uint8Array(await file.arrayBuffer());
      let binary = '';
      for (const byte of bytes) binary += String.fromCharCode(byte);
      const certificate = { fileName: file.name, base64: btoa(binary), password: this.password, uf: 'RS' as const, environment: 'homologacao' as const };
      this.api.validarCertificado(certificate).subscribe({
        next: result => {
          try {
            this.store.save({ ...certificate, validUntil: result.validade });
            this.validUntil.set(result.validade);
            this.showUpload.set(false);
            this.savedName.set(file.name);
            this.password = ''; this.file = null;
            if (this.fileInput) this.fileInput.value = '';
            this.message.set(`Certificado validado e salvo neste navegador. Válido até ${new Date(result.validade).toLocaleDateString('pt-BR')}.`);
          } catch { this.message.set('Não foi possível salvar. Verifique o armazenamento do navegador.'); }
          this.busy.set(false);
        },
        error: error => { this.message.set(error.error?.detail ?? 'Não foi possível validar o certificado. Verifique o backend e tente novamente.'); this.busy.set(false); },
      });
    } catch { this.message.set('Não foi possível ler o arquivo.'); this.busy.set(false); }
  }

  protected remove(): void {
    try {
      this.store.remove(); this.savedName.set(''); this.validUntil.set(''); this.password = ''; this.file = null;
      if (this.fileInput) this.fileInput.value = '';
      this.showUpload.set(true); this.message.set('Certificado e senha removidos deste navegador.');
    }
    catch { this.message.set('Não foi possível remover o certificado.'); }
  }

  protected testConnection(): void {
    if (this.busy()) return;
    try {
      const certificate = this.store.load();
      if (!certificate) { this.message.set('Cadastre um certificado antes de testar a conexão.'); return; }
      this.busy.set(true);
      this.api.consultarStatusSefaz(certificate).subscribe({
        next: result => { this.message.set(`SEFAZ/RS · Homologação: ${result.codigo} — ${result.motivo}. Esta consulta não emite nota fiscal.`); this.busy.set(false); },
        error: error => { this.message.set(error.error?.detail ?? 'Não foi possível consultar a SEFAZ/RS.'); this.busy.set(false); },
      });
    } catch { this.message.set('Não foi possível ler o certificado salvo. Cadastre novamente.'); }
  }
}
