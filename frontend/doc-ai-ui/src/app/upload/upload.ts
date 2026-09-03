import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Document } from '../models/document.model';
import { SearchResult } from '../models/search-result.model';
import { AskResponse } from '../models/ask-response.model';
import { Observable, BehaviorSubject, combineLatest, map, timeout, finalize } from 'rxjs';
import { FormsModule } from '@angular/forms';
import { DocumentService } from '../services/document';
import { AuthService } from '../services/auth';
import { Router } from '@angular/router';

@Component({
  selector: 'app-upload',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './upload.html',
  styleUrls: ['./upload.css']
})
export class UploadComponent implements OnInit {

  selectedFile: File | null = null;
  message = '';
  chatQuery = '';
  chatMessage = '';
  chatAnswer = '';
  chatSources: SearchResult[] = [];
  get uniqueSourceFileNames(): string[] {
    return Array.from(new Set(this.chatSources.map(s => s.fileName).filter(name => !!name)));
  }
  isAsking = false;
  private askWatchdog: ReturnType<typeof setTimeout> | null = null;

  private documentsSubject = new BehaviorSubject<Document[]>([]);
  documents$ = this.documentsSubject.asObservable();
  searchText$ = new BehaviorSubject<string>('');
  filteredDocuments$: Observable<Document[]>;
  semanticResults: SearchResult[] = [];
  loggedInUsername = '';
  isTextModalOpen = false;
  modalTitle = '';
  modalText = '';

  constructor(
    private documentService: DocumentService,
    private authService: AuthService,
    private router: Router,
    private cdr: ChangeDetectorRef
  ) {
    this.filteredDocuments$ = combineLatest([
      this.documents$,
      this.searchText$
    ]).pipe(
      map(([docs, search]) =>
        docs.filter(d =>
          d.fileName.toLowerCase().includes(search.toLowerCase()) ||
          (d.extractedText &&
            d.extractedText.toLowerCase().includes(search.toLowerCase()))
        )
      )
    );
  }

  ngOnInit(): void {
    this.loadLoggedInUser();
    this.loadDocuments();
  }

  private loadLoggedInUser() {
    const token = this.authService.getToken();
    if (!token) {
      this.loggedInUsername = '';
      return;
    }

    const payload = this.decodeJwtPayload(token);
    if (!payload) {
      this.loggedInUsername = '';
      return;
    }

    this.loggedInUsername =
      this.getStringClaim(payload, 'username') ||
      this.getStringClaim(payload, 'unique_name') ||
      this.getStringClaim(payload, 'name') ||
      this.getStringClaim(payload, 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name') ||
      '';
  }

  private decodeJwtPayload(token: string): Record<string, unknown> | null {
    try {
      const payloadPart = token.split('.')[1];
      if (!payloadPart) {
        return null;
      }

      const base64 = payloadPart
        .replace(/-/g, '+')
        .replace(/_/g, '/');
      const padding = (4 - (base64.length % 4)) % 4;
      const normalizedBase64 = `${base64}${'='.repeat(padding)}`;
      const decoded = atob(normalizedBase64);
      return JSON.parse(decoded) as Record<string, unknown>;
    } catch {
      return null;
    }
  }

  private getStringClaim(payload: Record<string, unknown>, claim: string): string {
    const claimValue = payload[claim];
    return typeof claimValue === 'string' ? claimValue : '';
  }

  // 🔄 Load documents and rebuild filter stream
  loadDocuments() {
    this.documentService.getDocuments().subscribe({
      next: (docs) => this.documentsSubject.next(docs),
      error: () => {
        this.message = 'Could not refresh documents list';
      }
    });
  }

  // 🔍 Search input change
  onSearchChange(value: string) {
    this.searchText$.next(value);
  }

  askQuestion() {
    if (this.isAsking) {
      return;
    }

    const query = this.chatQuery.trim();
    if (!query) {
      this.chatMessage = 'Enter a question to ask';
      this.chatAnswer = '';
      this.chatSources = [];
      return;
    }

    this.chatMessage = 'Thinking...';
    this.chatAnswer = '';
    this.chatSources = [];
    this.isAsking = true;
    this.clearAskWatchdog();
    this.askWatchdog = setTimeout(() => {
      if (!this.isAsking) {
        return;
      }

      this.isAsking = false;
      this.chatMessage = 'Request timed out. Please check backend and Ollama, then try again.';
      this.cdr.detectChanges();
    }, 32000);

    this.documentService.askQuestion(query).pipe(
      timeout(30000),
      finalize(() => {
        this.isAsking = false;
        this.clearAskWatchdog();
      })
    ).subscribe({
      next: (response: AskResponse) => {
        this.chatMessage = '';
        const normalizedAnswer = response?.answer ?? response?.Answer ?? '';
        const normalizedSources = response?.sources ?? response?.Sources ?? [];
        this.chatAnswer = typeof normalizedAnswer === 'string' ? normalizedAnswer : '';
        this.chatSources = Array.isArray(normalizedSources) ? normalizedSources : [];

        if (!this.chatAnswer) {
          this.chatMessage = 'No answer was returned by the server.';
        }

        this.cdr.detectChanges();
      },
      error: () => {
        this.chatMessage = 'Could not generate an answer. Please verify backend + Ollama are running.';
        this.chatAnswer = '';
        this.chatSources = [];
        this.cdr.detectChanges();
      }
    });
  }

  onFileSelected(event: any) {
    this.selectedFile = event.target.files[0];
  }

  upload() {
    if (!this.selectedFile) return;

    this.message = 'Uploading...';

    this.documentService.uploadDocument(this.selectedFile).subscribe({
      next: () => {
        this.message = 'Upload successful';
        this.selectedFile = null;
        this.loadDocuments();
        setTimeout(() => this.loadDocuments(), 1200);
      },
      error: () => this.message = 'Upload failed'
    });
  }

  logout() {
    this.authService.logout();
    this.router.navigate(['/']);
  }

  viewText(doc: Document) {
    this.modalTitle = doc.fileName;
    this.modalText = doc.extractedText || 'Text not extracted yet. Please try again later.';
    this.isTextModalOpen = true;
  }

  downloadDocument(doc: Document) {
    this.documentService.downloadDocument(doc.id).subscribe({
      next: (blob) => {
        const url = window.URL.createObjectURL(blob);
        const anchor = document.createElement('a');
        anchor.href = url;
        anchor.download = doc.fileName;
        anchor.click();
        window.URL.revokeObjectURL(url);
      },
      error: () => {
        this.message = 'Download failed';
      }
    });
  }

  closeTextModal() {
    this.isTextModalOpen = false;
    this.modalTitle = '';
    this.modalText = '';
  }

  private clearAskWatchdog() {
    if (this.askWatchdog) {
      clearTimeout(this.askWatchdog);
      this.askWatchdog = null;
    }
  }
}