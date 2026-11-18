import { DatePipe } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { Todo } from '../../core/api/todo.model';

@Component({
  selector: 'app-todo-card',
  standalone: true,
  imports: [DatePipe],
  templateUrl: './todo-card.html',
  styleUrl: './todo-card.scss'
})
export class TodoCard {
  @Input({ required: true }) item!: Todo;
  @Input() detail: Todo | null = null;
  @Input() busy = false;
  @Input() detailLoading = false;
  @Output() toggle = new EventEmitter<Todo>();
  @Output() view = new EventEmitter<Todo>();
  @Output() edit = new EventEmitter<Todo>();
  @Output() remove = new EventEmitter<Todo>();
}
