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
    this.loggedInUsername = this.authService.getUsername();
    this.authService.getCurrentUser().subscribe({
      next: (user) => {
        if (user?.username) {
          this.loggedInUsername = user.username;
          this.authService.saveUsername(user.username);
          this.cdr.detectChanges();
        }
      },
      error: () => {}
    });
  }

  loadDocuments() {
    this.documentService.getDocuments().subscribe({
      next: (docs) => this.documentsSubject.next(docs),
      error: () => {
        this.message = 'Could not refresh documents list';
      }
    });
  }

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
        this.cdr.detectChanges();
      })
    ).subscribe({
      next: (response: AskResponse) => {
        this.isAsking = false;
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
        this.isAsking = false;
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
    this.authService.logout().subscribe({
      next: () => this.router.navigate(['/']),
      error: () => this.router.navigate(['/'])
    });
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

  deleteDocument(doc: Document) {
    const confirmed = window.confirm(`Delete "${doc.fileName}"? This cannot be undone.`);
    if (!confirmed) {
      return;
    }

    this.documentService.deleteDocument(doc.id).subscribe({
      next: () => {
        this.message = 'Document deleted';
        this.loadDocuments();
      },
      error: () => {
        this.message = 'Delete failed';
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
