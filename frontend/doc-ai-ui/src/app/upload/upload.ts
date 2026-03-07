import { Component, OnInit } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { Document } from '../models/document.model';
import { Observable, BehaviorSubject, combineLatest, map } from 'rxjs';
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

  private documentsSubject = new BehaviorSubject<Document[]>([]);
  documents$ = this.documentsSubject.asObservable();
  searchText$ = new BehaviorSubject<string>('');
  filteredDocuments$: Observable<Document[]>;
  loggedInUsername = '';
  isTextModalOpen = false;
  modalTitle = '';
  modalText = '';

  constructor(
    private http: HttpClient,
    private documentService: DocumentService,
    private authService: AuthService,
    private router: Router
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

  onFileSelected(event: any) {
    this.selectedFile = event.target.files[0];
  }

  upload() {
    if (!this.selectedFile) return;

    this.message = 'Uploading...';

    const formData = new FormData();
    formData.append('file', this.selectedFile);

    this.http.post(
      'https://localhost:7018/api/Documents/upload',
      formData
    ).subscribe({
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

  closeTextModal() {
    this.isTextModalOpen = false;
    this.modalTitle = '';
    this.modalText = '';
  }
}