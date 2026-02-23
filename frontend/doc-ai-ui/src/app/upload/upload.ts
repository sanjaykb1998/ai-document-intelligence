import { Component, OnInit } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { DocumentService } from '../services/document';
import { CommonModule } from '@angular/common';
import { HttpClientModule } from '@angular/common/http';
import { Document } from '../models/document.model';
import { Observable, BehaviorSubject, combineLatest, map } from 'rxjs';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-upload',
  standalone: true,
  imports: [CommonModule, HttpClientModule, FormsModule],
  templateUrl: './upload.html',
  styleUrls: ['./upload.css']
})
export class UploadComponent implements OnInit {

  selectedFile: File | null = null;
  message = '';

  documents$!: Observable<Document[]>;
  searchText$ = new BehaviorSubject<string>('');
  filteredDocuments$!: Observable<Document[]>;

  constructor(
    private http: HttpClient,
    private documentService: DocumentService
  ) {}

  ngOnInit(): void {
    this.loadDocuments();
  }

  // 🔄 Load documents and rebuild filter stream
  loadDocuments() {
    this.documents$ = this.documentService.getDocuments();

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

  // 🔍 Search input change
  onSearchChange(value: string) {
    this.searchText$.next(value);
  }

  onFileSelected(event: any) {
    this.selectedFile = event.target.files[0];
  }

  upload() {
    if (!this.selectedFile) return;

    const formData = new FormData();
    formData.append('file', this.selectedFile);

    this.http.post(
      'https://localhost:7018/api/Documents/upload',
      formData
    ).subscribe({
      next: () => {
        this.message = 'Upload successful';
        this.selectedFile = null;
        this.loadDocuments(); // refresh after upload
      },
      error: () => this.message = 'Upload failed'
    });
  }

viewText(doc: Document) {
  if (!doc.extractedText) {
    alert('Text not extracted yet. Please try again later.');
  } else {
    alert(doc.extractedText);
  }
}
}